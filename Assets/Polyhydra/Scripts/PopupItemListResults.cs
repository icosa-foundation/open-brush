using System.Collections.Generic;
namespace TiltBrush
{
    public struct ItemListResults
    {
        public List<string> Items;
        public bool NextPageExists;
        public int ItemCount;

        public ItemListResults(List<string> items, bool nextPageExists)
        {
            Items = items;
            ItemCount = items.Count;
            NextPageExists = nextPageExists;
        }
    }
}
