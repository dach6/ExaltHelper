namespace ExaltHelper.Proxy.Networking.Packets;

internal class VaultContentPacket : Packet
{
	public bool IsLastVaultPacket;

	public int VaultChestObjectId;

	public int MaterialChestObjectId;

	public int GiftChestObjectId;

	public int PotionStorageObjectId;

	public int SeasonalSpoilChestObjectId;

	public int[] VaultContents;

	public int[] MaterialContents;

	public int[] GiftContents;

	public int[] PotionContents;

	public int[] SeasonalSpoilContents;

	public short VaultUpgradeCost;

	public short PotionUpgradeCost;

	public short CurrentPotionStorage;

	public short MaxPotionStorage;

	public short PotionStorageCost;

	public short UnknownShort1;

	public string UnknownString1;

	public string UnknownString2;

	public string UnknownString3;

	public override void Read(PacketReader reader)
	{
		IsLastVaultPacket = reader.ReadBoolean();
		VaultChestObjectId = reader.ReadCompressedInt();
		MaterialChestObjectId = reader.ReadCompressedInt();
		GiftChestObjectId = reader.ReadCompressedInt();
		PotionStorageObjectId = reader.ReadCompressedInt();
		SeasonalSpoilChestObjectId = reader.ReadCompressedInt();
		int num = reader.ReadCompressedInt();
		VaultContents = new int[num];
		for (int i = 0; i < num; i++)
		{
			VaultContents[i] = reader.ReadCompressedInt();
		}
		num = reader.ReadCompressedInt();
		MaterialContents = new int[num];
		for (int j = 0; j < num; j++)
		{
			MaterialContents[j] = reader.ReadCompressedInt();
		}
		num = reader.ReadCompressedInt();
		GiftContents = new int[num];
		for (int k = 0; k < num; k++)
		{
			GiftContents[k] = reader.ReadCompressedInt();
		}
		num = reader.ReadCompressedInt();
		PotionContents = new int[num];
		for (int l = 0; l < num; l++)
		{
			PotionContents[l] = reader.ReadCompressedInt();
		}
		num = reader.ReadCompressedInt();
		SeasonalSpoilContents = new int[num];
		for (int m = 0; m < num; m++)
		{
			SeasonalSpoilContents[m] = reader.ReadCompressedInt();
		}
		VaultUpgradeCost = reader.ReadInt16();
		PotionUpgradeCost = reader.ReadInt16();
		CurrentPotionStorage = reader.ReadInt16();
		MaxPotionStorage = reader.ReadInt16();
		PotionStorageCost = reader.ReadInt16();
		UnknownShort1 = reader.ReadInt16();
		UnknownString1 = reader.ReadString();
		UnknownString2 = reader.ReadString();
		UnknownString3 = reader.ReadString();
	}

	public override void Write(PacketWriter writer)
	{
		writer.Write(IsLastVaultPacket);
		writer.WriteCompressedInt(VaultChestObjectId);
		writer.WriteCompressedInt(MaterialChestObjectId);
		writer.WriteCompressedInt(GiftChestObjectId);
		writer.WriteCompressedInt(PotionStorageObjectId);
		writer.WriteCompressedInt(SeasonalSpoilChestObjectId);
		writer.WriteCompressedInt(VaultContents.Length);
		int[] array = VaultContents;
		foreach (int vaultItem in array)
		{
			writer.WriteCompressedInt(vaultItem);
		}
		writer.WriteCompressedInt(MaterialContents.Length);
		array = MaterialContents;
		foreach (int giftItem in array)
		{
			writer.WriteCompressedInt(giftItem);
		}
		writer.WriteCompressedInt(GiftContents.Length);
		array = GiftContents;
		foreach (int potionItem in array)
		{
			writer.WriteCompressedInt(potionItem);
		}
		writer.WriteCompressedInt(PotionContents.Length);
		array = PotionContents;
		foreach (int vaultItemData in array)
		{
			writer.WriteCompressedInt(vaultItemData);
		}
		writer.WriteCompressedInt(SeasonalSpoilContents.Length);
		array = SeasonalSpoilContents;
		foreach (int giftItemData in array)
		{
			writer.WriteCompressedInt(giftItemData);
		}
		writer.Write(VaultUpgradeCost);
		writer.Write(PotionUpgradeCost);
		writer.Write(CurrentPotionStorage);
		writer.Write(MaxPotionStorage);
		writer.Write(PotionStorageCost);
		writer.Write(UnknownShort1);
		writer.Write(UnknownString1);
		writer.Write(UnknownString2);
		writer.Write(UnknownString3);
	}
}
