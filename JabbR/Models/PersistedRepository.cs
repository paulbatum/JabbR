﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JabbR.Models
{
    public class PersistedRepository : IJabbrRepository
    {
        private readonly JabbrContext _db;

        public PersistedRepository(JabbrContext db)
        {
            _db = db;
        }

        public IQueryable<ChatRoom> Rooms
        {
            get { return _db.Rooms; }
        }

        public IQueryable<ChatUser> Users
        {
            get { return _db.Users; }
        }

        public void Add(ChatRoom room)
        {
            _db.Rooms.Add(room);
            _db.SaveChanges();
        }

        public void Add(ChatUser user)
        {
            _db.Users.Add(user);
            _db.SaveChanges();
        }

        public void Remove(ChatRoom room)
        {
            _db.Rooms.Remove(room);
            _db.SaveChanges();
        }

        public void Remove(ChatUser user)
        {
            _db.Users.Remove(user);
            _db.SaveChanges();
        }

        public void CommitChanges()
        {
            _db.SaveChanges();
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        public ChatUser GetUserById(string userId)
        {
            return _db.Users.FirstOrDefault(u => u.Id == userId);
        }

        public ChatUser GetUserByName(string userName)
        {
            return _db.Users.FirstOrDefault(u => u.Name == userName);
        }

        public ChatRoom GetRoomByName(string roomName)
        {
            return _db.Rooms.FirstOrDefault(r => r.Name == roomName);
        }

        public IQueryable<ChatUser> SearchUsers(string name)
        {
            return _db.Users.Online().Where(u => u.Name.Contains(name));
        }
    }
}