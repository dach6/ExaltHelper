namespace ExaltHelper.Proxy.DataStructures;

internal interface IGameEntity<TId>
{
	string Name { get; }

	TId ID { get; }
}
