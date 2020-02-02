using System;
using System.Collections.Generic;

namespace Marten.Migrator
{
    internal class ModelSnapshot
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public Type Type { get; set; }

        public List<ModelSnapshotProperty> Properties { get; set; }
    }
}
