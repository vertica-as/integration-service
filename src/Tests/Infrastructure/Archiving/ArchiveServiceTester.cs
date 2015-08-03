﻿using System.Data;
using NSubstitute;
using NUnit.Framework;
using Vertica.Integration.Infrastructure.Archiving;
using Vertica.Integration.Infrastructure.Database;

namespace Vertica.Integration.Tests.Infrastructure.Archiving
{
    [TestFixture]
    public class ArchiveServiceTester
    {
        [Test]
        public void ArchiveText_Verify_DbInteraction()
        {
            IDbSession session;
            ArchiveService subject = Initialize(out session);

            const int expectedId = 1;
            session.ExecuteScalar<int>(null).ReturnsForAnyArgs(expectedId);

            ArchiveCreated archive = subject.ArchiveText("C", new string('C', 10000));

            Assert.That(archive.Id, Is.EqualTo(expectedId.ToString()));
        }

        private ArchiveService Initialize(out IDbSession session)
        {
            var dbFactory = Substitute.For<IDbFactory>();
            session = Substitute.For<IDbSession>();

            dbFactory.OpenSession().Returns(session);

            IDbTransaction transaction = Substitute.For<IDbTransaction>();
            session.BeginTransaction().Returns(transaction);

            return new ArchiveService(() => dbFactory);
        }
    }
}