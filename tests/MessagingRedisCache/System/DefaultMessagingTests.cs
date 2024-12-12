using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MessagingRedisCache.Tests.System;

[TestClass]
public class DefaultMessagingTests() :
    MessagingTestsBase(
        MessagingType.Default)
{
    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
    }
}
