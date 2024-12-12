using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MessagingRedisCache.Tests.System;

[TestClass]
public class KeyeventMessagingTests() :
    MessagingTestsBase(
        MessagingType.KeyeventNotifications)
{
    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
    }
}
