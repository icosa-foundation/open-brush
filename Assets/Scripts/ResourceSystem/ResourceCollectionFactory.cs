using System;
using System.Collections.Generic;
using UnityEngine;
namespace TiltBrush
{
    public class ResourceCollectionFactory : MonoBehaviour
    {
        public static ResourceCollectionFactory Instance { get; private set; }

        private Dictionary<string, IResourceCollectionFactory> m_CollectionFactories;
        private Dictionary<Uri, IResourceCollection> m_Collections;

        private void Awake()
        {
            Instance = this;
            m_CollectionFactories = new Dictionary<string, IResourceCollectionFactory>();
            m_Collections = new Dictionary<Uri, IResourceCollection>();

            foreach (var factory in GetComponentsInChildren<IResourceCollectionFactory>())
            {
                RegisterCollectionType(factory);
            }
        }

        public void RegisterCollectionType(IResourceCollectionFactory factory)
        {
            m_CollectionFactories[factory.Scheme] = factory;
        }

        public IResourceCollection FetchCollection(string uri)
        {
            return FetchCollection(new Uri(uri));
        }

        public IResourceCollection FetchCollection(Uri uri)
        {
            if (m_Collections.TryGetValue(uri, out var collection))
            {
                return collection;
            }
            if (m_CollectionFactories.TryGetValue(uri.Scheme, out var factory))
            {
                var newCollection = factory.Create(uri);
                m_Collections[uri] = newCollection;
                return newCollection;
            }
            else
            {
                Debug.LogWarning($"{typeof(ResourceCollectionFactory).Name}: A handler for Uri scheme of '{uri.Scheme}' could not be found.");
                return null;
            }
        }

    }
}
