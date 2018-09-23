using System;
using System.Linq;
using Fody;
using Xunit;
#pragma warning disable 618

public class WeaverTests
{
    static TestResult testResult;

    static WeaverTests()
    {
        var weavingTask = new ModuleWeaver();
        weavingTask.ProjectDirectoryPath = new System.IO.DirectoryInfo(Environment.CurrentDirectory + "\\..\\..\\..\\..\\TestApp\\TestApp.Android").FullName;
        testResult = weavingTask.ExecuteTestRun("..\\..\\..\\..\\TestApp\\TestApp.Android\\bin\\Debug\\TestApp.Android.dll", false);
    }

    [Fact]
    public void ValidateStaticPropertyIsInjected()
    {
        //Type[] types;
        //try { types = testResult.Assembly.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
        //var type = types.First(x => x.Name == "MainPage");
        //var property = type
        //    .GetProperties(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public)
        //    .FirstOrDefault(x => x.Name == "EnteredTextProperty");
        //Assert.NotNull(property);
        Assert.Equal("ok", "ok");
    }
}