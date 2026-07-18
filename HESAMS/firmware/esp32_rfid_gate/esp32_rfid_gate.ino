/* =============================================================================
   HESAMS — ESP32 Gate Unit Firmware
   Hardware:  ESP32-WROOM-32 · MFRC522 / industrial Wiegand RFID reader
              · 30A automotive relay (ignition interlock) · buzzer · 2 LEDs
   Behaviour: card tap -> POST /api/access/validate -> relay only on unlock=true
              offline -> local whitelist cache (synced every 5 min)
              engine off / second tap -> POST /api/access/end-session
   Build:     Arduino IDE or PlatformIO (board: esp32dev)
   =============================================================================
   WIRING
     MFRC522 SDA->GPIO5  SCK->GPIO18  MOSI->GPIO23  MISO->GPIO19  RST->GPIO22
     Relay IN  -> GPIO26   (drives ignition-enable circuit through opto-isolator)
     Buzzer    -> GPIO27
     LED green -> GPIO25   LED red -> GPIO33
     NOTE: the relay must be wired in SERIES with the starter-enable line only.
           Never wire it into a safety-critical stop circuit.
   ========================================================================== */

#include <WiFi.h>
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include <SPI.h>
#include <MFRC522.h>
#include <Preferences.h>

// ---------------- CONFIGURATION ----------------
const char* WIFI_SSID     = "SITE-IOT";
const char* WIFI_PASSWORD = "********";
const char* API_BASE      = "https://hesams.company.com";
const char* READER_ID     = "RDR-A1";          // must match Equipment.RfidReaderId
const char* DEVICE_TOKEN  = "device-token-here"; // X-Device-Token header
const char* ROOT_CA       = R"(-----BEGIN CERTIFICATE-----
... paste your server/root CA certificate here ...
-----END CERTIFICATE-----)";

#define PIN_RELAY   26
#define PIN_BUZZER  27
#define PIN_LED_OK  25
#define PIN_LED_ERR 33
#define PIN_SS       5
#define PIN_RST     22

const unsigned long CACHE_SYNC_MS   = 5UL * 60UL * 1000UL;  // 5 min
const unsigned long HTTP_TIMEOUT_MS = 4000;
const int  MAX_CACHE = 64;

MFRC522 rfid(PIN_SS, PIN_RST);
Preferences prefs;

String  cachedUids[MAX_CACHE];
int     cacheCount = 0;
unsigned long lastSync = 0;
bool    engineRunning  = false;
String  activeUid      = "";
unsigned long sessionStart = 0;

// ---------------- HELPERS ----------------
void feedback(bool ok, const char* reason = nullptr) {
  digitalWrite(PIN_LED_OK,  ok ? HIGH : LOW);
  digitalWrite(PIN_LED_ERR, ok ? LOW  : HIGH);
  if (ok) { tone(PIN_BUZZER, 2000, 120); }
  else    { for (int i = 0; i < 3; i++) { tone(PIN_BUZZER, 400, 120); delay(180); } }
  Serial.printf("[GATE] %s%s%s\n", ok ? "GRANTED" : "DENIED",
                reason ? " - " : "", reason ? reason : "");
}

void unlockRelay() {
  digitalWrite(PIN_RELAY, HIGH);   // ignition enable
  engineRunning = true;
  sessionStart  = millis();
}

void lockRelay() {
  digitalWrite(PIN_RELAY, LOW);
  engineRunning = false;
  activeUid = "";
}

String readCardUid() {
  String uid = "";
  for (byte i = 0; i < rfid.uid.size; i++) {
    if (rfid.uid.uidByte[i] < 0x10) uid += "0";
    uid += String(rfid.uid.uidByte[i], HEX);
    if (i < rfid.uid.size - 1) uid += ":";
  }
  uid.toUpperCase();
  return uid;
}

// ---------------- OFFLINE CACHE ----------------
void loadCacheFromFlash() {
  prefs.begin("hesams", true);
  cacheCount = prefs.getInt("count", 0);
  for (int i = 0; i < cacheCount && i < MAX_CACHE; i++)
    cachedUids[i] = prefs.getString(("uid" + String(i)).c_str(), "");
  prefs.end();
  Serial.printf("[CACHE] loaded %d uids from flash\n", cacheCount);
}

void saveCacheToFlash() {
  prefs.begin("hesams", false);
  prefs.putInt("count", cacheCount);
  for (int i = 0; i < cacheCount; i++)
    prefs.putString(("uid" + String(i)).c_str(), cachedUids[i]);
  prefs.end();
}

/* Pull the pre-approved whitelist for THIS machine. The server returns only
   operators who pass every gate right now — the cache is a degraded fallback,
   never the primary decision path. */
void syncWhitelist() {
  if (WiFi.status() != WL_CONNECTED) return;
  WiFiClientSecure client; client.setCACert(ROOT_CA);
  HTTPClient http; http.setTimeout(HTTP_TIMEOUT_MS);
  String url = String(API_BASE) + "/api/access/whitelist?readerId=" + READER_ID;
  if (!http.begin(client, url)) return;
  http.addHeader("X-Device-Token", DEVICE_TOKEN);
  int code = http.GET();
  if (code == 200) {
    StaticJsonDocument<4096> doc;
    if (deserializeJson(doc, http.getString()) == DeserializationError::Ok) {
      cacheCount = 0;
      for (JsonVariant v : doc["uids"].as<JsonArray>()) {
        if (cacheCount >= MAX_CACHE) break;
        cachedUids[cacheCount++] = v.as<String>();
      }
      saveCacheToFlash();
      Serial.printf("[CACHE] synced %d uids\n", cacheCount);
    }
  }
  http.end();
  lastSync = millis();
}

bool inCache(const String& uid) {
  for (int i = 0; i < cacheCount; i++) if (cachedUids[i] == uid) return true;
  return false;
}

// ---------------- SERVER CALLS ----------------
/* returns: 1 = granted, 0 = denied, -1 = server unreachable */
int validateOnServer(const String& uid, String& reasonOut) {
  if (WiFi.status() != WL_CONNECTED) return -1;
  WiFiClientSecure client; client.setCACert(ROOT_CA);
  HTTPClient http; http.setTimeout(HTTP_TIMEOUT_MS);
  if (!http.begin(client, String(API_BASE) + "/api/access/validate")) return -1;
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Token", DEVICE_TOKEN);

  StaticJsonDocument<200> body;
  body["readerId"] = READER_ID;
  body["cardUid"]  = uid;
  String payload; serializeJson(body, payload);

  int code = http.POST(payload);
  int result = -1;
  if (code == 200) {
    StaticJsonDocument<1024> doc;
    if (deserializeJson(doc, http.getString()) == DeserializationError::Ok) {
      result = doc["unlock"].as<bool>() ? 1 : 0;
      reasonOut = doc["reason"] | "";
    }
  }
  http.end();
  return result;
}

void endSessionOnServer(float hours) {
  if (WiFi.status() != WL_CONNECTED) return;
  WiFiClientSecure client; client.setCACert(ROOT_CA);
  HTTPClient http; http.setTimeout(HTTP_TIMEOUT_MS);
  if (!http.begin(client, String(API_BASE) + "/api/access/end-session")) return;
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Token", DEVICE_TOKEN);
  StaticJsonDocument<200> body;
  body["readerId"]   = READER_ID;
  body["hoursAdded"] = hours;
  String payload; serializeJson(body, payload);
  http.POST(payload);
  http.end();
}

// ---------------- MAIN ----------------
void setup() {
  Serial.begin(115200);
  pinMode(PIN_RELAY, OUTPUT);  digitalWrite(PIN_RELAY, LOW); // fail-safe: locked
  pinMode(PIN_BUZZER, OUTPUT);
  pinMode(PIN_LED_OK, OUTPUT);
  pinMode(PIN_LED_ERR, OUTPUT);

  SPI.begin();
  rfid.PCD_Init();
  loadCacheFromFlash();

  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  for (int i = 0; i < 40 && WiFi.status() != WL_CONNECTED; i++) { delay(250); Serial.print("."); }
  Serial.println(WiFi.status() == WL_CONNECTED ? "\n[WIFI] connected" : "\n[WIFI] offline mode");
  syncWhitelist();
}

void loop() {
  if (millis() - lastSync > CACHE_SYNC_MS) syncWhitelist();

  if (!rfid.PICC_IsNewCardPresent() || !rfid.PICC_ReadCardSerial()) { delay(120); return; }
  String uid = readCardUid();
  Serial.printf("[SCAN] %s\n", uid.c_str());

  // Same card while running -> tap out, close session
  if (engineRunning && uid == activeUid) {
    float hours = (millis() - sessionStart) / 3600000.0f;
    lockRelay();
    endSessionOnServer(hours);
    feedback(true, "session closed");
    rfid.PICC_HaltA();
    delay(1200);
    return;
  }
  // Different card while running -> refuse takeover
  if (engineRunning) {
    feedback(false, "machine in use");
    rfid.PICC_HaltA();
    delay(1200);
    return;
  }

  String reason = "";
  int decision = validateOnServer(uid, reason);

  if (decision == -1) {                       // offline fallback
    if (inCache(uid)) { unlockRelay(); activeUid = uid; feedback(true, "offline cache"); }
    else              { feedback(false, "offline - not in cache"); }
  } else if (decision == 1) {
    unlockRelay(); activeUid = uid; feedback(true);
  } else {
    feedback(false, reason.c_str());          // server told us exactly why
  }

  rfid.PICC_HaltA();
  rfid.PCD_StopCrypto1();
  delay(1500);
  digitalWrite(PIN_LED_ERR, LOW);
}
