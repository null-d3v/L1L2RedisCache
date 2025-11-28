namespace MessagingRedisCache.Tests.System;

[TestClass]
public class KeyspaceMessagingTests(
    TestContext testContext) :
    MessagingTestsBase(
        MessagingType.KeyspaceNotifications,
        testContext)
{
    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
    }
}
