namespace Rota.Models
{
    /// <summary>
    /// Provides date normalization for absence requests.
    /// </summary>
    public static class AbsenceDateUtilities
    {
        /// <summary>
        /// Normalizes an absence start timestamp to midnight UTC for an all-day request.
        /// </summary>
        /// <param name="startDateUtc">The stored start timestamp.</param>
        /// <param name="sourceWasPartDay">True when the timestamp came from a part-day request.</param>
        /// <returns>The UTC-midnight start of the intended calendar date.</returns>
        public static DateTime NormalizeFullDayStartDateUtc(DateTime startDateUtc, bool sourceWasPartDay = false)
        {
            var utcStart = DateTime.SpecifyKind(startDateUtc, DateTimeKind.Utc);
            if (!sourceWasPartDay && utcStart.TimeOfDay == TimeSpan.Zero)
            {
                return utcStart;
            }

            var localDate = DateOnly.FromDateTime(utcStart.ToLocalTime());
            return DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        }
    }
}
