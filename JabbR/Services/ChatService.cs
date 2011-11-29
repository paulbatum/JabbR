﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using JabbR.Infrastructure;
using JabbR.Models;

namespace JabbR.Services
{
    public class ChatService : IChatService
    {
        private readonly IJabbrRepository _repository;

        public ChatService(IJabbrRepository repository)
        {
            _repository = repository;
        }

        public ChatUser AddUser(string userName, string clientId, string password)
        {
            if (!IsValidUserName(userName))
            {
                throw new InvalidOperationException(String.Format("'{0}' is not a valid user name.", userName));
            }

            EnsureUserNameIsAvailable(userName);

            var user = new ChatUser
            {
                Name = userName,
                Status = (int)UserStatus.Active,
                Id = Guid.NewGuid().ToString("d"),
                LastActivity = DateTime.UtcNow,
                ClientId = clientId
            };


            if (!String.IsNullOrEmpty(password))
            {
                ValidatePassword(password);
                user.HashedPassword = password.ToSha256();
            }

            _repository.Add(user);

            return user;
        }

        public void AuthenticateUser(string userName, string password)
        {
            ChatUser user = _repository.VerifyUser(userName);

            if (user.HashedPassword == null)
            {
                throw new InvalidOperationException(String.Format("The nick '{0}' is unclaimable", userName));
            }

            if (user.HashedPassword != password.ToSha256())
            {
                throw new InvalidOperationException(String.Format("Unable to claim '{0}'.", userName));
            }
        }

        public void ChangeUserName(ChatUser user, string newUserName)
        {
            if (!IsValidUserName(newUserName))
            {
                throw new InvalidOperationException(String.Format("'{0}' is not a valid user name.", newUserName));
            }

            if (user.Name.Equals(newUserName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("That's already your username...");
            }

            EnsureUserNameIsAvailable(newUserName);

            // Update the user name
            user.Name = newUserName;
        }

        public void SetUserPassword(ChatUser user, string password)
        {
            ValidatePassword(password);
            user.HashedPassword = password.ToSha256();
        }

        public void ChangeUserPassword(ChatUser user, string oldPassword, string newPassword)
        {
            if (user.HashedPassword != oldPassword.ToSha256())
            {
                throw new InvalidOperationException("Passwords don't match.");
            }

            ValidatePassword(newPassword);
            user.HashedPassword = newPassword.ToSha256();
        }

        public ChatRoom AddRoom(ChatUser user, string name)
        {
            if (name.Equals("Lobby", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Lobby is not a valid chat room.");
            }

            if (!IsValidRoomName(name))
            {
                throw new InvalidOperationException(String.Format("'{0}' is not a valid room name.", name));
            }

            var room = new ChatRoom
            {
                Name = name,
                Creator = user
            };

            room.Owners.Add(user);

            _repository.Add(room);

            user.OwnedRooms.Add(room);

            return room;
        }

        public void JoinRoom(ChatUser user, ChatRoom room)
        {
            // Add this room to the user's list of rooms
            user.Rooms.Add(room);

            // Add this user to the list of room's users
            room.Users.Add(user);
        }

        public void UpdateActivity(ChatUser user)
        {
            user.Status = (int)UserStatus.Active;
            user.LastActivity = DateTime.UtcNow;
        }

        public void LeaveRoom(ChatUser user, ChatRoom room)
        {
            // Remove the user from the room
            room.Users.Remove(user);

            // Remove this room from the users' list
            user.Rooms.Remove(room);
        }

        public ChatMessage AddMessage(ChatUser user, ChatRoom room, string content)
        {
            var chatMessage = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("d"),
                User = user,
                Content = content,
                When = DateTimeOffset.UtcNow
            };

            room.Messages.Add(chatMessage);

            return chatMessage;
        }

        public void AddOwner(ChatUser ownerOrCreator, ChatUser targetUser, ChatRoom targetRoom)
        {
            // Ensure the user is owner of the target room
            EnsureOwner(ownerOrCreator, targetRoom);

            if (targetRoom.Owners.Contains(targetUser))
            {
                // If the target user is already an owner, then throw
                throw new InvalidOperationException(String.Format("'{0}' is already and owner of '{1}'.", targetUser.Name, targetRoom.Name));
            }

            // Make the user an owner
            targetRoom.Owners.Add(targetUser);
            targetUser.OwnedRooms.Add(targetRoom);
        }

        public void KickUser(ChatUser user, ChatUser targetUser, ChatRoom targetRoom)
        {
            EnsureOwner(user, targetRoom);

            if (targetUser == user)
            {
                throw new InvalidOperationException("Why would you want to kick yourself?");
            }

            if (!IsUserInRoom(targetRoom, targetUser))
            {
                throw new InvalidOperationException(String.Format("'{0}' isn't in '{1}'.", targetUser.Name, targetRoom.Name));
            }

            // If this user isnt' the creator and the target user is an owner then throw
            if (targetRoom.Creator != user && targetRoom.Owners.Contains(targetUser))
            {
                throw new InvalidOperationException("Owners cannot kick other owners. Only the room creator and kick an owner.");
            }

            LeaveRoom(targetUser, targetRoom);
        }

        private void EnsureUserNameIsAvailable(string userName)
        {
            var userExists = _repository.Users.Any(u => u.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (userExists)
            {
                ThrowUserExists(userName);
            }
        }

        internal static void ThrowUserExists(string userName)
        {
            throw new InvalidOperationException(String.Format("Username {0} already taken, please pick a new one using '/nick nickname'.", userName));
        }

        internal static bool IsUserInRoom(ChatRoom room, ChatUser user)
        {
            return room.Users.Any(r => r.Name.Equals(user.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidatePassword(string password)
        {
            if (String.IsNullOrEmpty(password) || password.Length < 6)
            {
                throw new InvalidOperationException("Your password must be at least 6 characters.");
            }
        }

        private static bool IsValidUserName(string name)
        {
            return !String.IsNullOrEmpty(name) && Regex.IsMatch(name, "^[A-Za-z0-9-_.]{1,30}$");
        }

        private static bool IsValidRoomName(string name)
        {
            return !String.IsNullOrEmpty(name) && Regex.IsMatch(name, "^[A-Za-z0-9-_.]{1,30}$");
        }

        private static void EnsureOwner(ChatUser user, ChatRoom room)
        {
            if (!room.Owners.Contains(user))
            {
                throw new InvalidOperationException("You are not an owner of " + room.Name);
            }
        }
    }
}