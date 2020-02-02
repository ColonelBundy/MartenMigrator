namespace Marten.Migrator
{
    public class SyncOptions
    {
        /// <summary>
        /// Will also drop duplicated columns if the backing property was removed.
        /// </summary>
        public bool DropDuplicatedColumns { get; set; } = true;
    }
}
