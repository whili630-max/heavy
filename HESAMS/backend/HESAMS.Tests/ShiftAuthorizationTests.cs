using System;
using HESAMS.Api.Models;
using Xunit;

namespace HESAMS.Tests;

/// <summary>UT-15..UT-18: shift window logic, including overnight wrap-around.</summary>
public class ShiftAuthorizationTests
{
    private static bool InShift(TimeOnly start, TimeOnly end, TimeOnly now) =>
        start <= end ? now >= start && now <= end : now >= start || now <= end;

    [Theory]
    [InlineData("06:00", "18:00", "10:00", true)]   // UT-15 day shift, inside
    [InlineData("06:00", "18:00", "22:00", false)]  // UT-16 day shift, outside
    [InlineData("22:00", "06:00", "02:00", true)]   // UT-17 overnight, inside
    [InlineData("22:00", "06:00", "12:00", false)]  // overnight, outside
    [InlineData("22:00", "06:00", "23:59", true)]   // overnight, before midnight
    [InlineData("22:00", "06:00", "05:59", true)]   // overnight, just before end
    public void ShiftWindow_HandlesOvernightWrap(string s, string e, string now, bool expected) =>
        Assert.Equal(expected, InShift(TimeOnly.Parse(s), TimeOnly.Parse(e), TimeOnly.Parse(now)));
}
