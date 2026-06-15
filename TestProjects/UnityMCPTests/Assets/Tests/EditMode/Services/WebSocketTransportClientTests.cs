using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MCPForUnity.Editor.Services.Transport.Transports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MCPForUnityTests.Editor.Services
{
    [TestFixture]
    public class WebSocketTransportClientTests
    {
        private const string CandidateBuilderMethodName = "BuildConnectionCandidateUris";
        private const string WebSocketTransportClientTypeName = "MCPForUnity.Editor.Services.Transport.Transports.WebSocketTransportClient";
        private static readonly MethodInfo BuildConnectionCandidateUrisMethod = ResolveCandidateBuilderMethod();

        // -----------------------------------------------------------------------
        // Helpers shared by liveness / ApplyWelcome tests
        // -----------------------------------------------------------------------

        private static MethodInfo GetPrivateInstanceMethod(string name)
        {
            return typeof(WebSocketTransportClient).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static FieldInfo GetPrivateInstanceField(string name)
        {
            return typeof(WebSocketTransportClient).GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static FieldInfo GetPrivateStaticField(string name)
        {
            return typeof(WebSocketTransportClient).GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        /// <summary>
        /// Creates a bare <see cref="WebSocketTransportClient"/> (no tool-discovery service)
        /// that can be used to invoke private instance methods via reflection.
        /// </summary>
        private static WebSocketTransportClient CreateBareClient()
        {
            return new WebSocketTransportClient(toolDiscoveryService: null);
        }

        // -----------------------------------------------------------------------
        // DefaultInboundLivenessTimeout constant
        // -----------------------------------------------------------------------

        [Test]
        public void DefaultInboundLivenessTimeout_Is40Seconds()
        {
            // The constant is private static; verify it has the value introduced by the PR.
            FieldInfo field = GetPrivateStaticField("DefaultInboundLivenessTimeout");
            Assert.IsNotNull(field, "DefaultInboundLivenessTimeout field should exist");

            var value = (TimeSpan)field.GetValue(null);
            Assert.AreEqual(40.0, value.TotalSeconds, 0.001,
                "DefaultInboundLivenessTimeout should be 40 seconds");
        }

        [Test]
        public void InboundLivenessTimeout_DefaultsToDefaultInboundLivenessTimeout_OnNewInstance()
        {
            // A freshly-constructed client should use the default 40-second liveness timeout
            // before any welcome message arrives.
            using var client = CreateBareClient();

            FieldInfo field = GetPrivateInstanceField("_inboundLivenessTimeout");
            Assert.IsNotNull(field, "_inboundLivenessTimeout field should exist");

            var value = (TimeSpan)field.GetValue(client);
            Assert.AreEqual(40.0, value.TotalSeconds, 0.001,
                "_inboundLivenessTimeout should equal DefaultInboundLivenessTimeout on construction");
        }

        // -----------------------------------------------------------------------
        // ApplyWelcome — liveness timeout calculation
        // -----------------------------------------------------------------------

        /// <summary>
        /// Invokes the private <c>ApplyWelcome</c> method on <paramref name="client"/> and
        /// returns the resulting <c>_inboundLivenessTimeout</c> value.
        /// </summary>
        private static TimeSpan InvokeApplyWelcomeAndGetLivenessTimeout(
            WebSocketTransportClient client, JObject payload)
        {
            MethodInfo method = GetPrivateInstanceMethod("ApplyWelcome");
            Assert.IsNotNull(method, "ApplyWelcome method should exist");

            method.Invoke(client, new object[] { payload });

            FieldInfo livenessField = GetPrivateInstanceField("_inboundLivenessTimeout");
            Assert.IsNotNull(livenessField, "_inboundLivenessTimeout field should exist");

            return (TimeSpan)livenessField.GetValue(client);
        }

        [Test]
        public void ApplyWelcome_LivenessTimeout_IsKeepAliveIntervalTimes2Point5_WhenResultExceeds30s()
        {
            // keepAliveInterval = 15 s → 15 * 2.5 = 37.5 s  (> 30 s floor)
            using var client = CreateBareClient();
            var payload = new JObject { ["keepAliveInterval"] = 15 };

            TimeSpan liveness = InvokeApplyWelcomeAndGetLivenessTimeout(client, payload);

            Assert.AreEqual(37.5, liveness.TotalSeconds, 0.001,
                "Liveness timeout should be keepAliveInterval × 2.5 when result > 30 s");
        }

        [Test]
        public void ApplyWelcome_LivenessTimeout_IsFlooredAt30s_WhenKeepAliveIntervalIsSmall()
        {
            // keepAliveInterval = 10 s → 10 * 2.5 = 25 s  (< 30 s → floor to 30 s)
            using var client = CreateBareClient();
            var payload = new JObject { ["keepAliveInterval"] = 10 };

            TimeSpan liveness = InvokeApplyWelcomeAndGetLivenessTimeout(client, payload);

            Assert.AreEqual(30.0, liveness.TotalSeconds, 0.001,
                "Liveness timeout should be floored at 30 s when keepAliveInterval × 2.5 < 30 s");
        }

        [Test]
        public void ApplyWelcome_LivenessTimeout_IsFlooredAt30s_AtExactBoundary12Seconds()
        {
            // keepAliveInterval = 12 s → 12 * 2.5 = 30.0 s  (exactly at the boundary)
            using var client = CreateBareClient();
            var payload = new JObject { ["keepAliveInterval"] = 12 };

            TimeSpan liveness = InvokeApplyWelcomeAndGetLivenessTimeout(client, payload);

            Assert.AreEqual(30.0, liveness.TotalSeconds, 0.001,
                "Liveness timeout should equal 30 s at the exact boundary (12 s × 2.5 = 30 s)");
        }

        [Test]
        public void ApplyWelcome_LivenessTimeout_ScalesWithLargeKeepAliveInterval()
        {
            // keepAliveInterval = 60 s → 60 * 2.5 = 150 s
            using var client = CreateBareClient();
            var payload = new JObject { ["keepAliveInterval"] = 60 };

            TimeSpan liveness = InvokeApplyWelcomeAndGetLivenessTimeout(client, payload);

            Assert.AreEqual(150.0, liveness.TotalSeconds, 0.001,
                "Liveness timeout should scale with large keepAliveInterval values");
        }

        [Test]
        public void ApplyWelcome_LivenessTimeout_UsesDefaultKeepAlive_WhenPayloadHasNoKeepAliveInterval()
        {
            // No keepAliveInterval in payload → default 15 s keep-alive is kept.
            // 15 * 2.5 = 37.5 s > 30 s → liveness = 37.5 s
            using var client = CreateBareClient();
            var payload = new JObject(); // no keepAliveInterval key

            TimeSpan liveness = InvokeApplyWelcomeAndGetLivenessTimeout(client, payload);

            Assert.AreEqual(37.5, liveness.TotalSeconds, 0.001,
                "When no keepAliveInterval is present the default 15 s keep-alive yields 37.5 s liveness");
        }

        [Test]
        public void ApplyWelcome_LivenessTimeout_IsFlooredAt30s_WhenPayloadKeepAliveIsZeroOrNegative()
        {
            // keepAliveInterval = 0 → not applied (guard `> 0`); default 15 s is kept → 37.5 s.
            // But let's also test very small positive value: keepAliveInterval = 1 s → 1 * 2.5 = 2.5 s → floor 30 s.
            using var client = CreateBareClient();
            var payload = new JObject { ["keepAliveInterval"] = 1 };

            TimeSpan liveness = InvokeApplyWelcomeAndGetLivenessTimeout(client, payload);

            Assert.AreEqual(30.0, liveness.TotalSeconds, 0.001,
                "Liveness timeout should be floored at 30 s even when keepAliveInterval is very small");
        }

        [Test]
        public void ApplyWelcome_LivenessTimeout_IgnoresZeroKeepAliveInterval_UsesDefault()
        {
            // A zero keepAliveInterval in the payload should be ignored by the guard `> 0`.
            // The default 15 s keep-alive stays → liveness = 37.5 s.
            using var client = CreateBareClient();
            var payload = new JObject { ["keepAliveInterval"] = 0 };

            TimeSpan liveness = InvokeApplyWelcomeAndGetLivenessTimeout(client, payload);

            Assert.AreEqual(37.5, liveness.TotalSeconds, 0.001,
                "keepAliveInterval = 0 should be ignored; default 15 s keep-alive yields 37.5 s liveness");
        }

        // -----------------------------------------------------------------------
        // _lastInboundUtcTicks field seeding
        // -----------------------------------------------------------------------

        [Test]
        public void LastInboundUtcTicks_FieldExists()
        {
            // Verify that the backing field introduced by the PR is present on the type.
            FieldInfo field = GetPrivateInstanceField("_lastInboundUtcTicks");
            Assert.IsNotNull(field, "_lastInboundUtcTicks field should exist");
            Assert.AreEqual(typeof(long), field.FieldType,
                "_lastInboundUtcTicks should be of type long");
        }

        [Test]
        public void LastInboundUtcTicks_IsZero_OnNewInstance()
        {
            // Before StartBackgroundLoops is called the field should be its default (0).
            using var client = CreateBareClient();

            FieldInfo field = GetPrivateInstanceField("_lastInboundUtcTicks");
            Assert.IsNotNull(field, "_lastInboundUtcTicks field should exist");

            long ticks = (long)field.GetValue(client);
            Assert.AreEqual(0L, ticks,
                "_lastInboundUtcTicks should be zero before StartBackgroundLoops is called");
        }

        // -----------------------------------------------------------------------
        // ForceStop — public method that acquired a log call in this PR
        // -----------------------------------------------------------------------

        [Test]
        public void ForceStop_DoesNotThrow_WhenCalledWithNoActiveConnection()
        {
            // ForceStop is synchronous and must be safe to call even without a live socket.
            using var client = CreateBareClient();

            Assert.DoesNotThrow(() => client.ForceStop(),
                "ForceStop should not throw when there is no active connection");
        }

        [Test]
        public void ForceStop_SetsIsConnectedToFalse()
        {
            using var client = CreateBareClient();

            // ForceStop must ensure IsConnected is false afterwards.
            client.ForceStop();

            Assert.IsFalse(client.IsConnected,
                "IsConnected should be false after ForceStop");
        }

        [Test]
        public void ForceStop_IsIdempotent_CalledMultipleTimes()
        {
            // Calling ForceStop more than once must never throw.
            using var client = CreateBareClient();

            Assert.DoesNotThrow(() =>
            {
                client.ForceStop();
                client.ForceStop();
                client.ForceStop();
            }, "ForceStop should be idempotent and not throw on repeated calls");
        }

        [Test]
        public void ForceStop_SetsStateToDisconnected()
        {
            using var client = CreateBareClient();

            client.ForceStop();

            Assert.IsFalse(client.State.IsConnected,
                "Transport state should be disconnected after ForceStop");
        }

        // -----------------------------------------------------------------------
        // ReceiveLoopAsync — improved WebSocketException error message format
        // -----------------------------------------------------------------------

        [Test]
        public void WebSocketExceptionErrorMessageFormat_IncludesErrorCodeAndMessage()
        {
            // The PR changed the log / HandleSocketClosureAsync call to pass:
            //   $"{wse.WebSocketErrorCode}: {wse.Message}"
            // We verify this format string by constructing the same interpolation
            // from a known WebSocketException and checking the result matches expectations.
            var wse = new System.Net.WebSockets.WebSocketException(
                System.Net.WebSockets.WebSocketError.ConnectionClosedPrematurely,
                "remote party closed the connection");

            string formatted = $"{wse.WebSocketErrorCode}: {wse.Message}";

            StringAssert.Contains("ConnectionClosedPrematurely", formatted,
                "Formatted error string should include the WebSocketErrorCode");
            StringAssert.Contains("remote party closed the connection", formatted,
                "Formatted error string should include the original exception message");
        }

        [Test]
        public void WebSocketExceptionInnerExceptionFormat_IsIncludedWhenPresent()
        {
            // The PR constructs an inner-exception suffix only when InnerException != null:
            //   $" (inner: {wse.InnerException.GetType().Name}: {wse.InnerException.Message})"
            var inner = new System.IO.IOException("pipe broken");
            var wse = new System.Net.WebSockets.WebSocketException(
                System.Net.WebSockets.WebSocketError.ConnectionClosedPrematurely,
                inner);

            string innerSuffix = wse.InnerException != null
                ? $" (inner: {wse.InnerException.GetType().Name}: {wse.InnerException.Message})"
                : string.Empty;

            Assert.IsNotEmpty(innerSuffix,
                "Inner suffix should be non-empty when InnerException is present");
            StringAssert.Contains("IOException", innerSuffix,
                "Inner suffix should contain the inner exception type name");
            StringAssert.Contains("pipe broken", innerSuffix,
                "Inner suffix should contain the inner exception message");
        }

        [Test]
        public void WebSocketExceptionInnerExceptionFormat_IsEmptyWhenAbsent()
        {
            // When there is no inner exception the suffix must be empty string.
            var wse = new System.Net.WebSockets.WebSocketException(
                System.Net.WebSockets.WebSocketError.ConnectionClosedPrematurely,
                "no inner");

            string innerSuffix = wse.InnerException != null
                ? $" (inner: {wse.InnerException.GetType().Name}: {wse.InnerException.Message})"
                : string.Empty;

            Assert.IsEmpty(innerSuffix,
                "Inner suffix should be empty string when there is no inner exception");
        }

        [Test]
        public void BuildConnectionCandidateUris_NullEndpoint_ReturnsEmptyList()
        {

            List<Uri> candidates = InvokeBuildConnectionCandidateUris(null);

            // Assert
            Assert.IsNotNull(candidates);
            Assert.AreEqual(0, candidates.Count);
        }

        [Test]
        public void BuildConnectionCandidateUris_NonLocalhost_ReturnsOriginalOnly()
        {
            // Arrange
            var endpoint = new Uri("ws://127.0.0.1:8080/hub/plugin");

            // Act
            List<Uri> candidates = InvokeBuildConnectionCandidateUris(endpoint);

            // Assert
            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(endpoint, candidates[0]);
        }

        [Test]
        public void BuildConnectionCandidateUris_Localhost_AddsIPv4AndIPv6Fallbacks()
        {
            // Arrange
            var endpoint = new Uri("ws://localhost:8080/hub/plugin");

            // Act
            List<Uri> candidates = InvokeBuildConnectionCandidateUris(endpoint);

            // Assert
            Assert.AreEqual(3, candidates.Count);
            CollectionAssert.AreEqual(
                new[] { "localhost", "127.0.0.1", "::1" },
                candidates.Select(uri => NormalizeHostForComparison(uri.Host)).ToArray());

            int uniqueCount = candidates
                .Select(uri => uri.AbsoluteUri)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            Assert.AreEqual(candidates.Count, uniqueCount, "Fallback list should not contain duplicate endpoints.");
        }

        [Test]
        public void BuildConnectionCandidateUris_LocalhostFallbacks_PreserveSchemePortPathAndQuery()
        {
            // Arrange
            var endpoint = new Uri("wss://localhost:9443/custom/path?mode=test");

            // Act
            List<Uri> candidates = InvokeBuildConnectionCandidateUris(endpoint);

            // Assert
            Assert.AreEqual(3, candidates.Count);
            foreach (Uri candidate in candidates)
            {
                Assert.AreEqual("wss", candidate.Scheme);
                Assert.AreEqual(9443, candidate.Port);
                Assert.AreEqual("/custom/path", candidate.AbsolutePath);
                Assert.AreEqual("?mode=test", candidate.Query);
            }
        }

        private static List<Uri> InvokeBuildConnectionCandidateUris(Uri endpoint)
        {
            if (BuildConnectionCandidateUrisMethod == null)
            {
                Assert.Fail(BuildMissingMethodDiagnostic());
            }
            var result = BuildConnectionCandidateUrisMethod.Invoke(null, new object[] { endpoint });
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<List<Uri>>(result);
            return (List<Uri>)result;
        }

        private static MethodInfo ResolveCandidateBuilderMethod()
        {
            MethodInfo direct = GetCandidateBuilderMethod(typeof(WebSocketTransportClient));
            if (direct != null)
            {
                return direct;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type candidateType = assembly.GetType(WebSocketTransportClientTypeName);
                if (candidateType == null)
                {
                    continue;
                }

                MethodInfo method = GetCandidateBuilderMethod(candidateType);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo GetCandidateBuilderMethod(Type type)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            MethodInfo direct = type.GetMethod(
                CandidateBuilderMethodName,
                flags,
                binder: null,
                types: new[] { typeof(Uri) },
                modifiers: null);
            if (direct != null)
            {
                return direct;
            }

            // Fallback for environments where signature binding can differ between loaded copies.
            return type.GetMethods(flags).FirstOrDefault(method =>
            {
                if (!string.Equals(method.Name, CandidateBuilderMethodName, StringComparison.Ordinal))
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(Uri);
            });
        }

        private static string BuildMissingMethodDiagnostic()
        {
            var sb = new StringBuilder();
            sb.Append("Expected private candidate builder method to exist. Searched loaded assemblies for ")
              .Append(WebSocketTransportClientTypeName)
              .Append('.')
              .Append(CandidateBuilderMethodName)
              .Append(". Loaded candidate types:");

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type candidateType = assembly.GetType(WebSocketTransportClientTypeName);
                if (candidateType == null)
                {
                    continue;
                }

                sb.Append("\n- ")
                  .Append(assembly.FullName)
                  .Append(" @ ")
                  .Append(string.IsNullOrEmpty(assembly.Location) ? "<dynamic>" : assembly.Location);
            }

            return sb.ToString();
        }

        private static string NormalizeHostForComparison(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return host;
            }

            return host.Trim('[', ']');
        }
    }
}
