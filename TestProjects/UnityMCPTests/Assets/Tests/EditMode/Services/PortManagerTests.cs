using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnityTests.Editor.Services
{
    [TestFixture]
    public class PortManagerTests
    {
        [Test]
        public void IsPortAvailable_ReturnsFalse_WhenPortIsOccupied()
        {
            // Bind a port with ExclusiveAddressUse to simulate the real listener
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            try
            {
                Assert.IsFalse(PortManager.IsPortAvailable(port),
                    "IsPortAvailable should return false for a port that is already bound");
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void IsPortAvailable_ReturnsTrue_WhenPortIsFree()
        {
            // Bind and immediately release to get a port that was recently free
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            Assert.IsTrue(PortManager.IsPortAvailable(port),
                "IsPortAvailable should return true for a port that is not bound");
        }

#if UNITY_EDITOR_OSX
        [Test]
        public void IsPortAvailable_ReturnsFalse_WhenPortHeldWithReuseAddr()
        {
            // Simulate what AssetImportWorkers do: bind with SO_REUSEADDR.
            // IsPortAvailable must still detect this as occupied.
            var holder = new TcpListener(IPAddress.Loopback, 0);
            holder.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            holder.Start();
            int port = ((IPEndPoint)holder.LocalEndpoint).Port;

            try
            {
                Assert.IsFalse(PortManager.IsPortAvailable(port),
                    "IsPortAvailable should detect ports held with SO_REUSEADDR on macOS");
            }
            finally
            {
                holder.Stop();
            }
        }
#endif

        [Test]
        public void DiscoverNewPort_ReturnsAvailablePort()
        {
            int port = PortManager.DiscoverNewPort();
            Assert.Greater(port, 0, "DiscoverNewPort should return a positive port number");
            Assert.IsTrue(PortManager.IsPortAvailable(port),
                "The port returned by DiscoverNewPort should be available");
        }

        [Test]
        public void DiscoverNewPort_SkipsOccupiedDefaultPort()
        {
            // Hold the default port (6400) so DiscoverNewPort must find an alternative
            TcpListener holder = null;
            try
            {
                holder = new TcpListener(IPAddress.Loopback, 6400);
#if UNITY_EDITOR_OSX
                try { holder.Server.ExclusiveAddressUse = true; } catch { }
#endif
                holder.Start();
            }
            catch (SocketException)
            {
                // Port 6400 already occupied (e.g., by the running bridge) — that's fine,
                // the test still validates that DiscoverNewPort picks a different port.
                holder = null;
            }

            try
            {
                int port = PortManager.DiscoverNewPort();
                Assert.AreNotEqual(6400, port,
                    "DiscoverNewPort should not return the default port when it is occupied");
                Assert.Greater(port, 0);
            }
            finally
            {
                holder?.Stop();
            }
        }
    }
}
