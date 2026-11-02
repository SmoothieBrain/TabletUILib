using System;
using System.Collections.Generic;
using System.Linq;
using Alta.Networking;
using Alta.Serialization;
using MelonLoader;
using TavernLib.ModdingApi;
using TavernLib.Utils;
using UnityEngine;

namespace TabletUILib
{
    public static class ServerMods
    {
        const MessageType Ask = (MessageType)180;
        const MessageType Reply = (MessageType)181;
        const MessageType Press = (MessageType)182;

        static HashSet<string> mods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static HashSet<string> actions = new HashSet<string>();
        static Connection server;
        static Queue<KeyValuePair<Connection, string>> presses = new Queue<KeyValuePair<Connection, string>>();
        static int tries;
        static float nextAsk;

        public static bool Known { get; private set; }

        public static bool Has(string mod)
        {
            return Known && mod != null && mods.Contains(mod);
        }

        internal static bool HasAction(string id)
        {
            return Known && actions.Contains(id);
        }

        internal static void Run(string id)
        {
            if (server == null || server.IsDisposed || !HasAction(id)) return;
            server.Send(null, Press, (c, s) => s.SerializeString(ref id, Stream.StringEncoding.UTF8));
        }

        internal static void Start()
        {
            TavernEvents.SocketCreated.Subscribe(OnSocket);
        }

        internal static void Tick()
        {
            RunPresses();
            if (Known || tries >= 5 || server == null || server.IsDisposed || !server.IsApproved || Player.Current == null) return;
            if (Time.unscaledTime < nextAsk) return;
            tries++;
            nextAsk = Time.unscaledTime + 3f;
            server.Send(null, Ask, (c, s) => { });
        }

        static void OnSocket(ISocket socket)
        {
            socket.ConnectionCreated += c => OnConnection(socket, c);
            socket.ConnectionDestroyed += c =>
            {
                if (c == server) Forget();
            };
        }

        static void OnConnection(ISocket socket, Connection connection)
        {
            if (socket.IsServer)
            {
                connection.SetHandler(Ask, OnAsk);
                connection.SetHandler(Press, OnPress);
                return;
            }
            Forget();
            server = connection;
            connection.SetHandler(Reply, OnReply);
        }

        static void Forget()
        {
            server = null;
            tries = 0;
            nextAsk = 0f;
            Known = false;
            mods.Clear();
            actions.Clear();
        }

        static void OnAsk(Connection connection, Stream stream)
        {
            var names = MelonBase.RegisteredMelons.Select(m => m.Info.Name).Distinct().ToList();
            var ids = TabletWheel.ServerEntries.Select(e => e.Id).Distinct().ToList();
            connection.Send(null, Reply, (c, s) =>
            {
                Write(s, names);
                Write(s, ids);
            });
        }

        static void OnPress(Connection connection, Stream stream)
        {
            string id = "";
            stream.SerializeString(ref id, Stream.StringEncoding.UTF8);
            lock (presses) presses.Enqueue(new KeyValuePair<Connection, string>(connection, id));
        }

        static void RunPresses()
        {
            while (true)
            {
                KeyValuePair<Connection, string> press;
                lock (presses)
                {
                    if (presses.Count == 0) return;
                    press = presses.Dequeue();
                }
                if (press.Key.IsDisposed) continue;
                RunPress(press.Key, press.Value);
            }
        }

        static void RunPress(Connection connection, string id)
        {
            var entry = TabletWheel.ServerEntries.FirstOrDefault(e => e.Id == id);
            var player = connection.Player as Player;
            if (entry == null || player == null) return;
            if (entry.Role != null && !Roles.HasRole(connection, entry.Role))
            {
                MelonLogger.Msg(connection.UserInfo?.Username + " pressed " + id + " without the " + entry.Role + " role");
                return;
            }
            try
            {
                entry.Server(player);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("server button " + id + " broke: " + e);
            }
        }

        static void Write(Stream s, List<string> list)
        {
            int count = list.Count;
            s.SerializeInteger(ref count);
            for (int i = 0; i < count; i++)
            {
                string item = list[i];
                s.SerializeString(ref item, Stream.StringEncoding.UTF8);
            }
        }

        static List<string> Read(Stream s)
        {
            int count = 0;
            s.SerializeInteger(ref count);
            var list = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string item = "";
                s.SerializeString(ref item, Stream.StringEncoding.UTF8);
                list.Add(item);
            }
            return list;
        }

        static void OnReply(Connection connection, Stream stream)
        {
            mods = new HashSet<string>(Read(stream), StringComparer.OrdinalIgnoreCase);
            actions = new HashSet<string>(Read(stream));
            Known = true;
            MelonLogger.Msg("server has " + mods.Count + " mods and " + actions.Count + " server buttons");
        }
    }
}
