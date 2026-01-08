#if USE_ROSLYN
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using MCPForUnity.Editor.Tools;

namespace MCPForUnity.Editor.Tests
{
    public class ManageScriptRoslynFormattingTests
    {
        private static MethodInfo GetPrivate(string name)
        {
            return typeof(ManageScript).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        }

        [Test]
        public void GetRoslynType_UnknownType_ReturnsNull()
        {
            var method = GetPrivate("GetRoslynType");
            Assert.NotNull(method);
            var result = method.Invoke(null, new object[] { "MCPForUnity.DoesNotExist" });
            Assert.IsNull(result);
        }

        [Test]
        public void TryFormatWithRoslyn_DoesNotThrowAndReturnsTextIfAvailable()
        {
            var method = GetPrivate("TryFormatWithRoslyn");
            Assert.NotNull(method);

            var root = CSharpSyntaxTree.ParseText("class C{void M(){int x=1;}}").GetRoot();
            string formatted = null;
            Assert.DoesNotThrow(() =>
            {
                formatted = (string)method.Invoke(null, new object[] { root });
            });

            if (!string.IsNullOrEmpty(formatted))
            {
                StringAssert.Contains("class C", formatted);
                StringAssert.Contains("void M()", formatted);
            }
        }
    }
}
#endif
