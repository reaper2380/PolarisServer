﻿using System;
using System.IO;
using System.Linq;

namespace PolarisServer.Packets.Handlers
{
    [PacketHandlerAttr(0x11, 0x0)]
    public class Login : PacketHandler
    {
        public Login()
        {
        }

        public override void handlePacket(Client context, byte[] data, uint position, uint size)
        {
            // Parse the stuff we received
            var reader = new Packets.PacketReader(data, position, size);

            reader.BaseStream.Seek(0x2C, SeekOrigin.Current);

            uint macCount = reader.ReadMagic(0x5E6, 107);
            reader.BaseStream.Seek(0x1C * macCount, SeekOrigin.Current);

            reader.BaseStream.Seek(0x114, SeekOrigin.Current);

            var username = reader.ReadFixedLengthASCII(64);
            var password = reader.ReadFixedLengthASCII(64);

            // What am I doing here even
            var db = PolarisApp.Instance.Database;
            var users = from u in db.Players
                        where u.Username == username
                        select u;
            
            string error = "";
            Database.Player user = null;

            if (users.Count() == 0)
            {
                // Check if there is an empty field
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    error = "Username and password fields must not be empty.";
                    user = null;
                }
                // Check for special characters
                else if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-zA-Z0-9 ]*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    error = "Username must not contain special characters.\nPlease use letters and numbers only.";
                    user = null;
                }
                // We're all good!
                else
                {
                    // Insert new player into database
                    user = new Database.Player
                    {
                        Nickname = username,
                        Username = username,
                        Password = BCrypt.Net.BCrypt.HashPassword(password),
                        SettingsINI = System.IO.File.ReadAllText("settings.txt")
                    };
                    db.Players.Add(user);
                    db.SaveChanges();
                }
            }
            else
            {
                user = users.First();
                if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
                {
                    error = "Incorrect password.";
                    user = null;
                }
            }


            // Mystery packet
            var mystery = new Packets.PacketWriter();
            mystery.Write((uint)100);
            //SendPacket (0x11, 0x49, 0, mystery.ToArray ());

            // Login response packet
            var resp = new Packets.PacketWriter();
            resp.Write((uint)((user == null) ? 1 : 0)); // Status flag: 0=success, 1=error
            resp.WriteUTF16(error, 0x8BA4, 0xB6);

            if (user == null)
            {
                for (int i = 0; i < 0xEC; i++)
                    resp.Write((byte)0);
                context.SendPacket(0x11, 1, 4, resp.ToArray());
                return;
            }



            resp.Write((uint)user.PlayerID); // Player ID
            resp.Write((uint)0); // Unknown
            resp.Write((uint)0); // Unknown
            resp.WriteFixedLengthUTF16("B001-DarkFox", 0x20);
            for (int i = 0; i < 0xBC; i++)
                resp.Write((byte)0);

            context.SendPacket(0x11, 1, 4, resp.ToArray());

            // Settings packet
            var settings = new Packets.PacketWriter();
            settings.WriteASCII(user.SettingsINI, 0x54AF, 0x100);
            context.SendPacket(0x2B, 2, 4, settings.ToArray());

            context.User = user;
        }
    }
}

