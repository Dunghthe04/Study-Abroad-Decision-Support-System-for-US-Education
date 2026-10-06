using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Grading
{
    public class Gpa4OnlyConverter : IGpaConverter
    {
        public Task<decimal?> ToGpa4Async(decimal? gpa, string? gradeScale, CancellationToken ct = default)
        {
            decimal? result = gpa is { } g && gradeScale?.Trim() == "4" ? Math.Clamp(g, 0m, 4m) : null;
            return Task.FromResult(result);
        }
    }
}
