using System;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.Mods;
using ExaltHelper.Proxy.Networking;
using ExaltHelper.Proxy.Networking.Packets;

namespace ExaltHelper;

internal static class HudObserver
{
    private static DateTime lastWarning;
    private static Client activeClient;
    public static bool Accepts(Client client) => activeClient == null || ReferenceEquals(activeClient, client);
    public static void Observe(Client client, Packet packet, bool fromClient)
    {
        if (fromClient && packet is HelloPacket) activeClient = client;
        if (!Accepts(client)) return;
        Attempt(() => AccountWorkspace.ObservePacket(client, packet, fromClient));
        Attempt(() => DpsTrackerMod.ObserveAccount(client, AccountWorkspace.AccountKey));
        if (fromClient && packet is HelloPacket && string.IsNullOrEmpty(AccountWorkspace.AccountKey)) HudJournal.BeginConnection();
        Attempt(() => MissionWorkspace.ObservePacket(client, packet, fromClient));
        Attempt(() => { HudJournal.UseAccount(AccountWorkspace.AccountKey); HudJournal.ObservePacket(client, packet, fromClient); });
    }
    private static void Attempt(Action observe)
    {
        try { observe(); }
        catch (Exception)
        {
            if ((DateTime.UtcNow - lastWarning).TotalMinutes < 1) return;
            lastWarning = DateTime.UtcNow;
            HudJournal.AppendFeed("Notice", "Some dashboard data could not be read. Reconnect or refresh account data to resync.");
        }
    }
}
