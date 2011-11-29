﻿using System.Linq;
using System.Collections.Generic;
using System;

namespace JabbR.Models
{
    public static class RepositoryExtensions
    {
        public static IQueryable<ChatUser> Online(this IQueryable<ChatUser> source)
        {
            return source.Where(u => u.Status != (int)UserStatus.Offline);
        }

        public static IEnumerable<ChatUser> Online(this IEnumerable<ChatUser> source)
        {
            return source.Where(u => u.Status != (int)UserStatus.Offline);
        }

        public static ChatUser GetUserByClientId(this IJabbrRepository repository, string clientId)
        {
            return repository.Users.FirstOrDefault(u => u.ClientId == clientId);
        }

        public static ChatRoom VerifyUserRoom(this IJabbrRepository repository, ChatUser user, string roomName)
        {            
            if (String.IsNullOrEmpty(roomName))
            {
                throw new InvalidOperationException("Use '/join room' to join a room.");
            }

            ChatRoom room = repository.GetRoomByName(roomName);

            if (room == null)
            {
                throw new InvalidOperationException(String.Format("You're in '{0}' but it doesn't exist.", roomName));
            }

            if (!room.Users.Any(u => u.Name.Equals(user.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(String.Format("You're not in '{0}'. Use '/join {0}' to join it.", roomName));
            }

            return room;
        }

        public static ChatUser VerifyUserId(this IJabbrRepository repository, string userId)
        {
            ChatUser user = repository.GetUserById(userId);

            if (user == null)
            {
                throw new InvalidOperationException("You don't have a name. Pick a name using '/nick nickname'.");
            }

            return user;
        }

        public static ChatRoom VerifyRoom(this IJabbrRepository repository, string roomName)
        {
            if (String.IsNullOrWhiteSpace(roomName))
            {
                throw new InvalidOperationException("Room name cannot be blank!");
            }

            var room = repository.GetRoomByName(roomName);

            if (room == null)
            {
                throw new InvalidOperationException(String.Format("Unable to find room '{0}'.", roomName));
            }

            return room;
        }

        public static ChatUser VerifyUser(this IJabbrRepository repository, string userName)
        {
            ChatUser user = repository.GetUserByName(userName);

            if (user == null)
            {
                throw new InvalidOperationException(String.Format("Unable to find user '{0}'.", userName));
            }

            return user;
        }

    }
}