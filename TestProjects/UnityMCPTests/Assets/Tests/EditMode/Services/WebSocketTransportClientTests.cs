using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MCPForUnity.Editor.Services.Transport.Transports;
using NUnit.Framework;

namespace MCPForUnityTests.Editor.Services
{
    [TestFixture]
    public class WebSocketTransportClientTests
    {
        private const string CandidateBuilderMethodName = "BuildConnectionCandidateUris";
        private const string WebSocketTransportClientTypeName = "MCPForUnity.Editor.Services.Transport.Transports.WebSocketTransportClient";
        private static readonly MethodInfo BuildConnectionCandidateUrisMethod = ResolveCandidateBuilderMethod();

        [Test]
        public void BuildConnectionCandidateUris_NullEndpoint_ReturnsEmptyList()
        {
            // Act
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

        [Test]
        public void ComputeInboundLivenessTimeout_ScalesKeepAliveByTwoAndAHalf()
        {
            TimeSpan result = InvokeComputeInboundLivenessTimeout(TimeSpan.FromSeconds(15));

            // 15s * 2.5 = 37.5s, comfortably above the 30s floor.
            Assert.AreEqual(37.5, result.TotalSeconds, 0.0001);
        }

        [Test]
        public void ComputeInboundLivenessTimeout_AppliesThirtySecondFloor()
        {
            // 5s * 2.5 = 12.5s, which the floor lifts to 30s.
            TimeSpan small = InvokeComputeInboundLivenessTimeout(TimeSpan.FromSeconds(5));
            Assert.AreEqual(30.0, small.TotalSeconds, 0.0001);

            // A zero/degenerate cadence must still produce the floor, never zero.
            TimeSpan zero = InvokeComputeInboundLivenessTimeout(TimeSpan.Zero);
            Assert.AreEqual(30.0, zero.TotalSeconds, 0.0001);
        }

        [Test]
        public void ComputeInboundLivenessTimeout_LargeCadenceScalesAboveFloor()
        {
            // 60s * 2.5 = 150s, well above the floor.
            TimeSpan result = InvokeComputeInboundLivenessTimeout(TimeSpan.FromSeconds(60));
            Assert.AreEqual(150.0, result.TotalSeconds, 0.0001);
        }

        [Test]
        public void ShouldTripLivenessWatchdog_SilencePastTimeoutWithNoCommand_Trips()
        {
            bool trip = InvokeShouldTripLivenessWatchdog(
                TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(38), commandsInFlight: 0);

            Assert.IsTrue(trip);
        }

        [Test]
        public void ShouldTripLivenessWatchdog_CommandInFlight_DoesNotTrip()
        {
            // The core false-trip guard: an in-flight command parks the receive loop, so the
            // inbound silence is expected and must not be treated as a dead socket.
            bool oneInFlight = InvokeShouldTripLivenessWatchdog(
                TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(38), commandsInFlight: 1);
            Assert.IsFalse(oneInFlight);

            bool manyInFlight = InvokeShouldTripLivenessWatchdog(
                TimeSpan.FromSeconds(5000), TimeSpan.FromSeconds(38), commandsInFlight: 3);
            Assert.IsFalse(manyInFlight);
        }

        [Test]
        public void ShouldTripLivenessWatchdog_WithinTimeout_DoesNotTrip()
        {
            bool underTimeout = InvokeShouldTripLivenessWatchdog(
                TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(38), commandsInFlight: 0);
            Assert.IsFalse(underTimeout);

            // Exactly at the timeout is not past it (strictly-greater comparison).
            bool atTimeout = InvokeShouldTripLivenessWatchdog(
                TimeSpan.FromSeconds(38), TimeSpan.FromSeconds(38), commandsInFlight: 0);
            Assert.IsFalse(atTimeout);
        }

        private static TimeSpan InvokeComputeInboundLivenessTimeout(TimeSpan keepAliveInterval)
        {
            MethodInfo method = ResolveStaticMethod("ComputeInboundLivenessTimeout", typeof(TimeSpan));
            if (method == null)
            {
                Assert.Fail("Expected private static ComputeInboundLivenessTimeout(TimeSpan) to exist.");
            }
            object result = method.Invoke(null, new object[] { keepAliveInterval });
            Assert.IsInstanceOf<TimeSpan>(result);
            return (TimeSpan)result;
        }

        private static bool InvokeShouldTripLivenessWatchdog(TimeSpan sinceInbound, TimeSpan livenessTimeout, int commandsInFlight)
        {
            MethodInfo method = ResolveStaticMethod(
                "ShouldTripLivenessWatchdog", typeof(TimeSpan), typeof(TimeSpan), typeof(int));
            if (method == null)
            {
                Assert.Fail("Expected private static ShouldTripLivenessWatchdog(TimeSpan, TimeSpan, int) to exist.");
            }
            object result = method.Invoke(null, new object[] { sinceInbound, livenessTimeout, commandsInFlight });
            Assert.IsInstanceOf<bool>(result);
            return (bool)result;
        }

        private static MethodInfo ResolveStaticMethod(string name, params Type[] parameterTypes)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

            MethodInfo direct = typeof(WebSocketTransportClient).GetMethod(name, flags, binder: null, types: parameterTypes, modifiers: null);
            if (direct != null)
            {
                return direct;
            }

            // Fallback across loaded assemblies, mirroring candidate-builder resolution for
            // environments where multiple copies of the type may be loaded.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type candidateType = assembly.GetType(WebSocketTransportClientTypeName);
                MethodInfo method = candidateType?.GetMethod(name, flags, binder: null, types: parameterTypes, modifiers: null);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
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
