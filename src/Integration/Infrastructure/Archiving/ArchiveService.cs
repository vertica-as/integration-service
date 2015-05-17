﻿using System;
using System.Data;
using System.Linq;
using Vertica.Integration.Infrastructure.Database;
using Vertica.Integration.Infrastructure.Extensions;
using Vertica.Utilities_v4;

namespace Vertica.Integration.Infrastructure.Archiving
{
    public class ArchiveService : IArchiveService
    {
        private readonly IDbFactory _db;

        public ArchiveService(IDbFactory db)
        {
            _db = db;
        }

        public BeginArchive Create(string name, Action<ArchiveCreated> onCreated)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException(@"Value cannot be null or empty.", "name");
            if (onCreated == null) throw new ArgumentNullException("onCreated");

            return new BeginArchive(stream =>
            {
                int archiveId;

                using (IDbSession session = _db.OpenSession())
                using (IDbTransaction transaction = session.BeginTransaction())
                {
                    byte[] binaryData = stream.ToArray();

                    archiveId = session.ExecuteScalar<int>(
                        "INSERT INTO Archive (Name, BinaryData, ByteSize, Created) VALUES (@name, @binaryData, @byteSize, @created);" +
                        "SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        new
                        {
                            name = name.MaxLength(255),
                            binaryData,
                            byteSize = binaryData.Length,
                            created = Time.UtcNow
                        });

                    transaction.Commit();
                }

                onCreated(new ArchiveCreated(archiveId.ToString()));
            });
        }

        public Archive[] GetAll()
        {
            using (IDbSession session = _db.OpenSession())
            {
                return session.Query<Archive>("SELECT Id, Name, ByteSize, Created FROM Archive")
                    .ToArray();
            }
        }

        public byte[] Get(string id)
        {
            int value;
            if (!Int32.TryParse(id, out value))
                return null;

            using (IDbSession session = _db.OpenSession())
            {
                return
                    session.Query<byte[]>("SELECT BinaryData FROM Archive WHERE Id = @Id", new { Id = value })
                        .SingleOrDefault();
            }
        }

        public int Delete(DateTimeOffset olderThan)
        {
            using (IDbSession session = _db.OpenSession())
            using (IDbTransaction transaction = session.BeginTransaction())
            {
                int count = session.Execute("DELETE FROM Archive WHERE Created <= @olderThan", new { olderThan });

                transaction.Commit();

                return count;
            }
        }
    }
}