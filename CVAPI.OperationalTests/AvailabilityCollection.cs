using CVAPI.OperationalTests.Reports;

namespace CVAPI.OperationalTests;

/// <summary>
/// Fixture der køres én gang efter alle tests i "Availability"-kollektionen.
/// Kalder Report.Flush() som skriver JSON og CSV til Reports/output/.
/// </summary>
public class AvailabilityReportFixture : IDisposable
{
    public void Dispose()
    {
        Report.Flush();
    }
}

[CollectionDefinition("Availability")]
public class AvailabilityCollection : ICollectionFixture<AvailabilityReportFixture> { }
