using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Marten.Services;
using Marten.Util;
using Npgsql;

namespace Marten.Migrator
{
    public static class Migrator
    {
        public static void SyncData(this DocumentStore store)
        {
            SyncData(store, new SyncOptions
            {
                DropDuplicatedColumns = true
            });
        }

        public static void SyncData(this DocumentStore store, SyncOptions options)
        {
            var session = store.OpenSession(new SessionOptions
            {
                IsolationLevel = System.Data.IsolationLevel.Serializable
            });
            var docs = store.Storage.AllDocumentMappings.ToList();
            var snapshots = session.Query<ModelSnapshot>().ToList();

            foreach (var item in docs)
            {
                List<ModelSnapshotProperty> remove = new List<ModelSnapshotProperty>(); // Properties to remove
                List<PropertyInfo> add = new List<PropertyInfo>(); // Properties to add
                List<PropertyInfo> update = new List<PropertyInfo>(); // Properties to update
                ModelSnapshot snapshot = snapshots.SingleOrDefault(x => x.Name == item.DocumentType.Name);

                // Snapshot does not exist, lets create one.
                if (snapshot == null)
                {
                    session.Store(new ModelSnapshot
                    {
                        Name = item.DocumentType.Name,
                        Type = item.DocumentType,
                        Properties = item.DocumentType.GetProperties().Select(x => new ModelSnapshotProperty
                        {
                            Name = x.Name,
                            Type = x.PropertyType
                        }).ToList()
                    });

                    continue;
                }

                // Get properties defined in the class.
                var localClassProperties = item.DocumentType.GetProperties();

                // Check for missmatch types or added properties in class
                foreach (var prop in localClassProperties)
                {
                    ModelSnapshotProperty snapshotProp = snapshot.Properties.SingleOrDefault(x => x.Name == prop.Name);

                    if (snapshotProp != null) // Property exist
                    {
                        // Property has expected type
                        if (prop.PropertyType == snapshotProp.Type)
                        {
                            continue;
                        }

                        // Check if we can convert the type, only valuetypes supported for now.
                        if (snapshotProp.Type.IsValueType && TryChangeType(Activator.CreateInstance(snapshotProp.Type), prop.PropertyType) != null)
                        {
                            // We can convert the data to the new type
                            // No data loss here
                            update.Add(prop);

                            continue;
                        }

                        // Remove the prop to re add with correct type
                        // this will result in data loss
                        remove.Add(snapshotProp);
                    }

                    // We need to add the new type if it missmatched or
                    // Need to add this property since it was added.
                    add.Add(prop);
                }

                // Check for removed properties in class
                foreach (var prop in snapshots.Where(x => !localClassProperties.Any(y => y.Name == x.Name && y.PropertyType == x.Type)).ToList())
                {
                    // Remove it from snapshot.
                    snapshot.Properties.Remove(snapshot.Properties.Find(x => x.Name == prop.Name && x.Type == prop.Type));
                }

                // Update property in snapshot
                foreach (var prop in update)
                {
                    snapshot.Properties.Find(x => x.Name == prop.Name).Type = prop.PropertyType;
                }

                // Remove properties from data
                // And remove property from snapshot
                foreach (var prop in remove)
                {
                    // Remove property from data
                    using (NpgsqlCommand cmd = CommandBuilder.BuildCommand(x =>
                    {
                        x.Append("update ");
                        x.Append(item.Table.QualifiedName);
                        x.Append(" set data = data - '");
                        x.Append(prop.Name);
                        x.Append("'");
                    }))
                    {
                        session.Connection.RunSql(cmd.CommandText);
                    }

                    // Drop duplicated column if it exists
                    if (options.DropDuplicatedColumns)
                    {
                        // Check if we have a duplicated column by the same name & type
                        Schema.DuplicatedField duplicated = item.DuplicatedFields.SingleOrDefault(x => x.MemberName == prop.Name && x.MemberType == prop.Type);

                        if (duplicated != null)
                        {
                            using (NpgsqlCommand cmd = CommandBuilder.BuildCommand(x =>
                            {
                                x.Append("ALTER TABLE ");
                                x.Append(item.Table.QualifiedName);
                                x.Append(" DROP COLUMN IF EXISTS ");
                                x.Append(duplicated.ColumnName);
                            }))
                            {
                                session.Connection.RunSql(cmd.CommandText);
                            }
                        }
                    }

                    // Remove property from snapshot
                    snapshot.Properties.Remove(snapshot.Properties.Find(x => x.Name == prop.Name && x.Type == prop.Type));
                }

                // Add property to snapshot
                foreach (var prop in add)
                {
                    snapshot.Properties.Add(new ModelSnapshotProperty
                    {
                        Name = prop.Name,
                        Type = prop.PropertyType
                    });
                }

                session.Update(snapshot);
            }

            session.SaveChanges();
        }

        private static object ChangeType(object value, Type conversion)
        {
            var type = conversion;

            if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                if (value == null)
                {
                    return null;
                }

                type = Nullable.GetUnderlyingType(type);
            }

            return Convert.ChangeType(value, type);
        }

        private static object TryChangeType(object value, Type conversionType)
        {
            object response = null;

            var isNotConvertible =
                conversionType == null
                    || value == null
                    || !(value is IConvertible)
                || value.GetType() != conversionType;

            if (!isNotConvertible)
            {
                return response;
            }

            try
            {
                response = ChangeType(value, conversionType);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
            {
                response = null;
            }
#pragma warning restore CA1031 // Do not catch general exception types

            return response;
        }
    }
}