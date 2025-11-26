using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MessagingRedisCache.Tests.System;

[TestClass]
public class KeyeventMessagingTests(
    TestContext testContext) :
    MessagingTestsBase(
        MessagingType.KeyeventNotifications,
        testContext)
{
    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
    }
}
