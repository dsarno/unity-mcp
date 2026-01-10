using NUnit.Framework;
using UnityEditor;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Dependencies.PlatformDetectors;

namespace MCPForUnityTests.Editor.Dependencies
{
    /// <summary>
    /// Tests for UV path override detection in PlatformDetectorBase.
    /// Verifies that DetectUv() respects PathResolverService overrides.
    /// </summary>
    public class PlatformDetectorUvOverrideTests
    {
        private string _originalOverride;
        private bool _hadOverride;

        [SetUp]
        public void SetUp()
        {
            // Save any existing override
            _hadOverride = EditorPrefs.HasKey(EditorPrefKeys.UvxPathOverride);
            if (_hadOverride)
            {
                _originalOverride = EditorPrefs.GetString(EditorPrefKeys.UvxPathOverride);
            }

            // Clear any override for clean test state
            EditorPrefs.DeleteKey(EditorPrefKeys.UvxPathOverride);
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original state
            if (_hadOverride)
            {
                EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, _originalOverride);
            }
            else
            {
                EditorPrefs.DeleteKey(EditorPrefKeys.UvxPathOverride);
            }
        }

        [Test]
        public void PathResolverService_HasUvxPathOverride_ReturnsFalse_WhenNoOverrideSet()
        {
            // Arrange
            EditorPrefs.DeleteKey(EditorPrefKeys.UvxPathOverride);
            var service = new PathResolverService();

            // Assert
            Assert.IsFalse(service.HasUvxPathOverride, "Should return false when no override is set");
        }

        [Test]
        public void PathResolverService_HasUvxPathOverride_ReturnsTrue_WhenOverrideSet()
        {
            // Arrange
            EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, "/some/path/to/uvx");
            var service = new PathResolverService();

            // Assert
            Assert.IsTrue(service.HasUvxPathOverride, "Should return true when override is set");
        }

        [Test]
        public void PathResolverService_GetUvxPath_ReturnsOverridePath_WhenOverrideSet()
        {
            // Arrange
            const string overridePath = "/custom/path/to/uvx";
            EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, overridePath);
            var service = new PathResolverService();

            // Act
            string result = service.GetUvxPath();

            // Assert
            Assert.AreEqual(overridePath, result, "Should return the override path when set");
        }

        [Test]
        public void PathResolverService_GetUvxPath_ReturnsDefaultOrDiscovered_WhenNoOverride()
        {
            // Arrange
            EditorPrefs.DeleteKey(EditorPrefKeys.UvxPathOverride);
            var service = new PathResolverService();

            // Act
            string result = service.GetUvxPath();

            // Assert
            Assert.IsNotNull(result, "Should return a non-null path");
            Assert.IsNotEmpty(result, "Should return a non-empty path");
            // The default should either be a discovered path or "uvx"
        }

        [Test]
        public void PathResolverService_ClearUvxPathOverride_RemovesOverride()
        {
            // Arrange
            EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, "/some/path");
            var service = new PathResolverService();
            Assert.IsTrue(service.HasUvxPathOverride, "Pre-condition: override should be set");

            // Act
            service.ClearUvxPathOverride();

            // Assert
            Assert.IsFalse(service.HasUvxPathOverride, "Override should be cleared");
        }

        [Test]
        public void DetectUv_UsesOverridePath_WhenOverrideIsSet()
        {
            // This test verifies that DetectUv() uses the override path.
            // Since we can't mock the actual uvx executable, we set an override
            // and verify the status message indicates the override was used.

            // Arrange
            const string overridePath = "/nonexistent/override/path/uvx";
            EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, overridePath);
            var detector = new TestPlatformDetector();

            // Act
            var status = detector.DetectUv();

            // Assert
            // When override is set but path is invalid, the error details should mention the override
            if (!status.IsAvailable)
            {
                Assert.IsTrue(
                    status.Details.Contains("override") || status.Details.Contains(overridePath),
                    $"Error details should mention override path. Got: {status.Details}");
            }
        }

        [Test]
        public void DetectUv_ShowsOverridePathInDetails_WhenOverrideIsValid()
        {
            // This test uses the real uvx if available with an override path
            // Skip if uvx isn't installed
            var service = new PathResolverService();

            // First clear any override to find real uvx
            EditorPrefs.DeleteKey(EditorPrefKeys.UvxPathOverride);
            string realUvxPath = service.GetUvxPath();

            // If uvx exists at a discoverable path, test override detection with it
            if (realUvxPath != "uvx" && System.IO.File.Exists(realUvxPath))
            {
                // Set the real path as override
                EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, realUvxPath);
                var detector = new TestPlatformDetector();

                // Act
                var status = detector.DetectUv();

                // Assert
                Assert.IsTrue(status.IsAvailable, "Should detect uvx when valid override is set");
                Assert.IsTrue(
                    status.Details.Contains("override") || status.Details.Contains("using override"),
                    $"Details should indicate override path was used. Got: {status.Details}");
                Assert.AreEqual(realUvxPath, status.Path, "Status path should be the override path");
            }
            else
            {
                // Skip test if no discoverable uvx exists
                Assert.Ignore("Test skipped: No uvx executable found at a discoverable path");
            }
        }

        [Test]
        public void DetectUv_FallsBackToPath_WhenNoOverrideSet()
        {
            // Arrange
            EditorPrefs.DeleteKey(EditorPrefKeys.UvxPathOverride);
            var detector = new TestPlatformDetector();

            // Act
            var status = detector.DetectUv();

            // Assert
            // If uvx is found in PATH, details should NOT mention "override"
            if (status.IsAvailable)
            {
                // Could be "Found uv X at path" or "Found uv X in PATH"
                Assert.IsFalse(
                    status.Details.Contains("override"),
                    $"Details should not mention override when none is set. Got: {status.Details}");
            }
            // If not available, that's also valid - uvx just isn't installed
        }

        /// <summary>
        /// Concrete test implementation of PlatformDetectorBase for testing.
        /// </summary>
        private class TestPlatformDetector : PlatformDetectorBase
        {
            public override string PlatformName => "Test";
            public override bool CanDetect => true;

            public override MCPForUnity.Editor.Dependencies.Models.DependencyStatus DetectPython()
            {
                return new MCPForUnity.Editor.Dependencies.Models.DependencyStatus("Python", false);
            }

            public override string GetPythonInstallUrl() => "https://python.org";
            public override string GetUvInstallUrl() => "https://docs.astral.sh/uv/";
            public override string GetInstallationRecommendations() => "Install uv";
        }
    }
}
