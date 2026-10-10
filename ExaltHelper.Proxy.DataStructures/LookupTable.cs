using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ExaltHelper.Proxy.DataStructures;

internal class LookupTable<TKey, TEntity> where TEntity : class, IGameEntity<TKey>
{
	[CompilerGenerated]
	private sealed class LookupTableKeyBinding
	{
		public string TargetItemName;

		internal bool MatchesName(KeyValuePair<TKey, TEntity> entry)
		{
			return entry.Value.Name == TargetItemName;
		}
	}

	[CompilerGenerated]
	private sealed class LookupTableValueBinding
	{
		public Func<TEntity, bool> Predicate;

		internal bool MatchesPredicate(KeyValuePair<TKey, TEntity> entry)
		{
			return Predicate(entry.Value);
		}
	}

	[CompilerGenerated]
	private Dictionary<TKey, TEntity> _map;

	public Dictionary<TKey, TEntity> Map
	{
		[CompilerGenerated]
		get
		{
			return _map;
		}
		[CompilerGenerated]
		private set
		{
			_map = value;
		}
	}

	private LookupTable()
	{
	}

	public LookupTable(Dictionary<TKey, TEntity> itemsById)
	{
		Map = itemsById;
	}

	public TEntity GetById(TKey id)
	{
		if (Map.TryGetValue(id, out TEntity value))
		{
			return value;
		}
		return null;
	}

	public TEntity GetByName(string name)
	{
		IEnumerable<KeyValuePair<TKey, TEntity>> source = Map.Where((KeyValuePair<TKey, TEntity> keyValuePair) => keyValuePair.Value.Name == name);
		if (source.Any())
		{
			return source.First().Value;
		}
		return null;
	}

	public TEntity Find(Func<TEntity, bool> predicate)
	{
		return Map.First((KeyValuePair<TKey, TEntity> keyValuePair) => predicate(keyValuePair.Value)).Value;
	}
}
