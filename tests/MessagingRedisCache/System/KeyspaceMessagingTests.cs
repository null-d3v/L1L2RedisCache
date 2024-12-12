using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MessagingRedisCache.Tests.System;

[TestClass]
public class KeyspaceMessagingTests() :
    MessagingTestsBase(
        MessagingType.KeyspaceNotifications)
{
    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
    }
}
