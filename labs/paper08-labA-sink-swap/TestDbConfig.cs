using System;

namespace UnitTestChoreography
{
    // Connection strings for the DB-backed tests are read from environment
    // variables so that no credentials are committed to the repository. Set
    // PUPPETEER_TEST_MYSQL / PUPPETEER_TEST_SQLSERVER locally to run these tests;
    // they are tagged Integration/FlakyInCI and self-skip when the server is
    // unreachable, so the default per-commit suite stays green without them.
    // The placeholder defaults intentionally carry no real password.
    internal static class TestDbConfig
    {
        private const string MySqlPlaceholder =
            "persistsecurityinfo=True;port=3306;Server=localhost;user id=root;password=CHANGE_ME;SslMode=none;AllowPublicKeyRetrieval=true";
        private const string SqlServerPlaceholder =
            "Server=localhost,1433;User Id=sa;Password=CHANGE_ME;TrustServerCertificate=true;Encrypt=false;Connection Timeout=10";

        internal static string MySqlServer =>
            Environment.GetEnvironmentVariable("PUPPETEER_TEST_MYSQL") ?? MySqlPlaceholder;

        internal static string SqlServer =>
            Environment.GetEnvironmentVariable("PUPPETEER_TEST_SQLSERVER") ?? SqlServerPlaceholder;

        internal static string MySqlFor(string database) =>
            MySqlServer + $";Database={database}";

        internal static string SqlServerFor(string database) =>
            SqlServer + $";Database={database}";
    }
}
