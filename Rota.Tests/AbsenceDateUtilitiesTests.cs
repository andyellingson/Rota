using NUnit.Framework;
using Rota.Models;

namespace Rota.Tests;

[TestFixture]
public class AbsenceDateUtilitiesTests
{
    [Test]
    public void NormalizeFullDayStartDateUtc_WithPartDayTimestamp_UsesLocalDateAtUtcMidnight()
    {
        var intendedDate = new DateOnly(2026, 12, 3);
        var localStart = intendedDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Local);
        var startUtc = localStart.ToUniversalTime();

        var normalized = AbsenceDateUtilities.NormalizeFullDayStartDateUtc(startUtc, sourceWasPartDay: true);

        Assert.That(normalized, Is.EqualTo(DateTime.SpecifyKind(intendedDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)));
    }

    [Test]
    public void NormalizeFullDayStartDateUtc_WithMidnightUtcStart_PreservesExistingDate()
    {
        var startUtc = new DateTime(2026, 12, 3, 0, 0, 0, DateTimeKind.Utc);

        var normalized = AbsenceDateUtilities.NormalizeFullDayStartDateUtc(startUtc);

        Assert.That(normalized, Is.EqualTo(startUtc));
    }
}
