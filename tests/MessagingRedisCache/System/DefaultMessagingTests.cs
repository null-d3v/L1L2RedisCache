namespace MessagingRedisCache.Tests.System;

[TestClass]
public class DefaultMessagingTests(
    TestContext testContext) :
    MessagingTestsBase(
        MessagingType.Default,
        testContext)
{
    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
    }
}
