using CVAPI.OperationalTests.Reports;

namespace CVAPI.OperationalTests;

/// <summary>
/// Fixture der køres én gang efter alle tests i "Operations"-kollektionen.
/// Kalder Report.Flush() som skriver JSON og CSV til TestResults/.
/// </summary>
public class OperationsReportFixture : IDisposable
{
    public void Dispose()
    {
        Report.Flush();
    }
}

[CollectionDefinition("Operations")]
public class OperationsCollection : ICollectionFixture<OperationsReportFixture> { }
