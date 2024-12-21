using LandsatReflectance.UI.Utils;

namespace LandsatReflectance.UI.Tests;

[TestFixture]
public class ResultTests
{
    [Test]
    public void TestOkAndMatch1()
    {
        var someResult = Result<string, Exception>.FromOk("some value");
        
        Assert.That(someResult.IsOk, Is.EqualTo(true));
        Assert.That(someResult.IsError, Is.EqualTo(false));

        var flattened = someResult.Match(
            okVal => okVal,
            _ => string.Empty);
            
        Assert.That(flattened, Is.EqualTo("some value"));
    }
    
    [Test]
    public void TestErrorAndMatch1()
    {
        var someResult = Result<object, string>.FromError("some error");
        
        Assert.That(someResult.IsOk, Is.EqualTo(false));
        Assert.That(someResult.IsError, Is.EqualTo(true));

        var flattened = someResult.Match(
            _ => string.Empty,
            errorValue => errorValue);
            
        Assert.That(flattened, Is.EqualTo("some error"));
    }

    [Test]
    public void TestOkBind1()
    {
        var someResult = Result<string, object>.FromOk("Hello,");
        var boundResult = someResult.Bind(str => Result<string, object>.FromOk($"{str} World!"));
        
        Assert.That(boundResult.IsOk, Is.EqualTo(true));
        Assert.That(boundResult.IsError, Is.EqualTo(false));

        var flattened = boundResult.Match(
            okVal => okVal,
            _ => string.Empty);
            
        Assert.That(flattened, Is.EqualTo("Hello, World!"));
    }
}
