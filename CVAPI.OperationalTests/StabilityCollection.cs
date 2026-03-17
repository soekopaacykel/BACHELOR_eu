using CVAPI.OperationalTests.Reports;

namespace CVAPI.OperationalTests;

/// <summary>
/// Fixture der køres én gang efter alle tests i "Stability"-kollektionen.
/// Kalder Report.Flush() som skriver JSON og CSV til Reports/output/.
/// </summary>
public class ReportFlushFixture : IDisposable
{
    public void Dispose()
    {
        Report.Flush();
    }
}

[CollectionDefinition("Stability")]
public class StabilityCollection : ICollectionFixture<ReportFlushFixture> { }
