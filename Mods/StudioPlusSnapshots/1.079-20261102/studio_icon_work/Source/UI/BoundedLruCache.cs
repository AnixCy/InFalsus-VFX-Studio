using System.Collections.Generic;
namespace InFalsusStudio.NativeEditorUI {
 // Recycle a single least-recently-used slot at capacity. Frequently drawn static
 // labels remain warm while transient time/FPS strings pass through the cache.
 internal sealed class BoundedLruCache<TKey,TValue> {
  sealed class Entry {internal TKey Key;internal TValue Value;}
  readonly int capacity;
  readonly Dictionary<TKey,LinkedListNode<Entry>> entries;
  readonly LinkedList<Entry> order=new LinkedList<Entry>();
  internal int Count {get{return entries.Count;}}
  internal BoundedLruCache(int limit){capacity=limit;entries=new Dictionary<TKey,LinkedListNode<Entry>>(limit);}
  internal bool TryGetValue(TKey key,out TValue value){
   LinkedListNode<Entry> node;if(!entries.TryGetValue(key,out node)){value=default(TValue);return false;}
   order.Remove(node);order.AddFirst(node);value=node.Value.Value;return true;
  }
  internal TValue this[TKey key]{set{
   LinkedListNode<Entry> node;
   if(entries.TryGetValue(key,out node)){order.Remove(node);node.Value.Value=value;order.AddFirst(node);return;}
   if(entries.Count==capacity){node=order.Last;order.RemoveLast();entries.Remove(node.Value.Key);node.Value.Key=key;node.Value.Value=value;}
   else node=new LinkedListNode<Entry>(new Entry{Key=key,Value=value});
   entries.Add(key,node);order.AddFirst(node);
  }}
  internal void Clear(){entries.Clear();order.Clear();}
 }
}
