﻿using System;
using System.Linq;
using JabbR.Infrastructure;
using JabbR.Models;
using JabbR.Services;

namespace JabbR.Commands
{
    public class CommandManager
    {
        private readonly string _clientId;
        private readonly string _userId;
        private readonly string _roomName;
        private readonly INotificationService _notificationService;
        private readonly IChatService _chatService;
        private readonly IJabbrRepository _repository;

        public CommandManager(string clientId,
                              string userId,
                              string roomName,
                              IChatService service,
                              IJabbrRepository repository,
                              INotificationService notificationService)
        {
            _clientId = clientId;
            _userId = userId;
            _roomName = roomName;
            _chatService = service;
            _repository = repository;
            _notificationService = notificationService;
        }

        public bool TryHandleCommand(string command)
        {
            command = command.Trim();
            if (!command.StartsWith("/"))
            {
                return false;
            }

            string[] parts = command.Substring(1).Split(' ');
            string commandName = parts[0];

            if (!TryHandleBaseCommand(commandName, parts) &&
                !TryHandleUserCommand(commandName, parts) &&
                !TryHandleRoomCommand(commandName, parts))
            {
                // If none of the commands are valid then throw an exception
                throw new InvalidOperationException(String.Format("'{0}' is not a valid command.", commandName));
            }

            return true;
        }

        // Commands that require a user and room
        private bool TryHandleRoomCommand(string commandName, string[] parts)
        {
            ChatUser user = _repository.VerifyUserId(_userId);
            ChatRoom room = _repository.VerifyUserRoom(user, _roomName);

            if (commandName.Equals("me", StringComparison.OrdinalIgnoreCase))
            {
                HandleMe(room, user, parts);
                return true;
            }
            else if (commandName.Equals("leave", StringComparison.OrdinalIgnoreCase))
            {
                HandleLeave(room, user);

                return true;
            }
            else if (commandName.Equals("nudge", StringComparison.OrdinalIgnoreCase))
            {
                HandleNudge(room, user, parts);

                return true;
            }
            else if (TryHandleOwnerCommand(user, room, commandName, parts))
            {
                return true;
            }

            return false;
        }

        // Commands that require the user to be the owner of the room
        private bool TryHandleOwnerCommand(ChatUser user, ChatRoom room, string commandName, string[] parts)
        {
            if (commandName.Equals("kick", StringComparison.OrdinalIgnoreCase))
            {
                HandleKick(user, room, parts);

                return true;
            }

            return false;
        }

        private bool TryHandleBaseCommand(string commandName, string[] parts)
        {
            if (commandName.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                HandleHelp();

                return true;
            }
            else if (commandName.Equals("nick", StringComparison.OrdinalIgnoreCase))
            {
                HandleNick(parts);

                return true;
            }

            return false;
        }

        // Commands that require a user name
        private bool TryHandleUserCommand(string commandName, string[] parts)
        {
            ChatUser user = _repository.VerifyUserId(_userId);

            if (commandName.Equals("rooms", StringComparison.OrdinalIgnoreCase))
            {
                HandleRooms();

                return true;
            }
            else if (commandName.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                HandleList(parts);
                return true;
            }
            else if (commandName.Equals("who", StringComparison.OrdinalIgnoreCase))
            {
                HandleWho(parts);
                return true;
            }
            else if (commandName.Equals("join", StringComparison.OrdinalIgnoreCase))
            {
                HandleJoin(user, parts);

                return true;
            }
            else if (commandName.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                HandleCreate(user, parts);

                return true;
            }
            else if (commandName.Equals("msg", StringComparison.OrdinalIgnoreCase))
            {
                HandleMsg(user, parts);

                return true;
            }
            else if (commandName.Equals("gravatar", StringComparison.OrdinalIgnoreCase))
            {
                HandleGravatar(user, parts);

                return true;
            }
            else if (commandName.Equals("leave", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
            {
                HandleLeave(user, parts);

                return true;
            }
            else if (commandName.Equals("nudge", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
            {
                HandleNudge(user, parts);

                return true;
            }
            else if (commandName.EndsWith("addowner", StringComparison.OrdinalIgnoreCase))
            {
                HandleAddOwner(user, parts);

                return true;
            }

            return false;
        }

        private void HandleAddOwner(ChatUser user, string[] parts)
        {
            if (parts.Length == 1)
            {
                throw new InvalidOperationException("Who do you want to make an owner?");
            }

            string targetUserName = parts[1];

            ChatUser targetUser = _repository.VerifyUser(targetUserName);

            if (parts.Length == 2)
            {
                throw new InvalidOperationException("Which room?");
            }

            string roomName = parts[2];
            ChatRoom targetRoom = _repository.VerifyRoom(roomName);

            _chatService.AddOwner(user, targetUser, targetRoom);

            _notificationService.OnOwnerAdded(targetUser, targetRoom);

            _repository.CommitChanges();
        }

        private void HandleKick(ChatUser user, ChatRoom room, string[] parts)
        {
            if (parts.Length == 1)
            {
                throw new InvalidOperationException("Who are you trying to kick?");
            }

            if (room.Users.Count == 1)
            {
                throw new InvalidOperationException("You're the only person in here...");
            }

            string targetUserName = parts[1];

            ChatUser targetUser = _repository.VerifyUser(targetUserName);

            _chatService.KickUser(user, targetUser, room);

            _notificationService.KickUser(room, targetUser);

            _repository.CommitChanges();
        }

        private void HandleWho(string[] parts)
        {
            if (parts.Length == 1)
            {
                _notificationService.ListUsers();
                return;
            }

            var name = NormalizeUserName(parts[1]);

            ChatUser user = _repository.GetUserByName(name);

            if (user != null)
            {
                _notificationService.ListRooms(user);
                return;
            }

            var users = _repository.SearchUsers(name);

            if (users.Count() == 1)
            {
                user = users.First();
                _notificationService.ListRooms(user);
            }
            else
            {
                _notificationService.ListUsers(users);
            }
        }

        private void HandleList(string[] parts)
        {
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("List users in which room?");
            }

            string roomName = parts[1];
            ChatRoom room = _repository.VerifyRoom(roomName);

            var names = room.Users.Online().Select(s => s.Name);

            _notificationService.ListUsers(room, names);
        }

        private void HandleHelp()
        {
            _notificationService.ShowHelp();
        }

        private void HandleLeave(ChatUser user, string[] parts)
        {
            string roomName = parts[1];
            ChatRoom room = _repository.VerifyRoom(roomName);

            HandleLeave(room, user);
        }

        private void HandleLeave(ChatRoom room, ChatUser user)
        {
            _chatService.LeaveRoom(user, room);

            _notificationService.LeaveRoom(user, room);

            _repository.CommitChanges();
        }

        private void HandleMe(ChatRoom room, ChatUser user, string[] parts)
        {
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("You what?");
            }

            var content = String.Join(" ", parts.Skip(1));

            _notificationService.OnSelfMessage(room, user, content);
        }

        private void HandleMsg(ChatUser user, string[] parts)
        {
            if (_repository.Users.Count() == 1)
            {
                throw new InvalidOperationException("You're the only person in here...");
            }

            if (parts.Length < 2 || String.IsNullOrWhiteSpace(parts[1]))
            {
                throw new InvalidOperationException("Who are you trying send a private message to?");
            }
            var toUserName = NormalizeUserName(parts[1]);
            ChatUser toUser = _repository.VerifyUser(toUserName);

            if (toUser == user)
            {
                throw new InvalidOperationException("You can't private message yourself!");
            }

            string messageText = String.Join(" ", parts.Skip(2)).Trim();

            if (String.IsNullOrEmpty(messageText))
            {
                throw new InvalidOperationException(String.Format("What did you want to say to '{0}'.", toUser.Name));
            }

            _notificationService.SendPrivateMessage(user, toUser, messageText);
        }

        private void HandleCreate(ChatUser user, string[] parts)
        {
            if (parts.Length == 1)
            {
                throw new InvalidOperationException("No room specified.");
            }

            string roomName = parts[1];
            if (String.IsNullOrWhiteSpace(roomName))
            {
                throw new InvalidOperationException("No room specified.");
            }

            ChatRoom room = _repository.GetRoomByName(roomName);

            if (room != null)
            {
                throw new InvalidOperationException(String.Format("The room '{0}' already exists", roomName));
            }

            // Create the room, then join it
            room = _chatService.AddRoom(user, roomName);

            JoinRoom(user, room);

            _repository.CommitChanges();
        }

        private void HandleJoin(ChatUser user, string[] parts)
        {
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("Join which room?");
            }

            // Create the room if it doesn't exist
            string roomName = parts[1];
            ChatRoom room = _repository.VerifyRoom(roomName);

            if (ChatService.IsUserInRoom(room, user))
            {
                throw new InvalidOperationException("You're already in that room!");
            }

            JoinRoom(user, room);
        }

        private void JoinRoom(ChatUser user, ChatRoom room)
        {
            _chatService.JoinRoom(user, room);

            _notificationService.JoinRoom(user, room);

            _repository.CommitChanges();
        }

        private void HandleGravatar(ChatUser user, string[] parts)
        {
            string email = String.Join(" ", parts.Skip(1));

            if (String.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException("Email was not specified!");
            }

            string hash = email.ToLowerInvariant().ToMD5();

            SetGravatar(user, hash);
        }

        private void SetGravatar(ChatUser user, string hash)
        {
            // Set user hash
            user.Hash = hash;
            
            _notificationService.ChangeGravatar(user);

            _repository.CommitChanges();
        }

        private void HandleRooms()
        {
            _notificationService.ShowRooms();
        }

        /// <summary>
        /// Used to reserve a nick name.
        /// /nick nickname - sets the user's name to nick name or creates a user with that name
        /// /nick nickname password - sets a password for the specified nick name (if the current user has that nick name)
        /// /nick nickname password newpassword - updates the password for the specified nick name (if the current user has that nick name)
        /// </summary>
        private void HandleNick(string[] parts)
        {
            if (parts.Length == 1)
            {
                throw new InvalidOperationException("No nick specified!");
            }

            string userName = parts[1];
            if (String.IsNullOrWhiteSpace(userName))
            {
                throw new InvalidOperationException("No nick specified!");
            }

            string password = null;
            if (parts.Length > 2)
            {
                password = parts[2];
            }

            string newPassword = null;
            if (parts.Length > 3)
            {
                newPassword = parts[3];
            }

            // See if there is a current user
            ChatUser user = _repository.GetUserById(_userId);

            if (user == null && String.IsNullOrEmpty(newPassword))
            {
                user = _repository.GetUserByName(userName);

                // There's a user with the name specified
                if (user != null)
                {
                    if (String.IsNullOrEmpty(password))
                    {
                        ChatService.ThrowUserExists(userName);
                    }
                    else
                    {
                        // If there's no user but there's a password then authenticate the user
                        _chatService.AuthenticateUser(userName, password);

                        // TODO: Handle multple clients per user
                        user.ClientId = _clientId;
                    }
                }
                else
                {
                    // If there's no user add a new one
                    user = _chatService.AddUser(userName, _clientId, password);
                }

                // Notify the user that they're good to go!
                _notificationService.OnUserCreated(user);
            }
            else
            {
                if (String.IsNullOrEmpty(password))
                {
                    string oldUserName = user.Name;

                    // Change the user's name
                    _chatService.ChangeUserName(user, userName);

                    _notificationService.OnUserNameChanged(user, userName, oldUserName);
                }
                else
                {
                    // If the user specified a password, verify they own the nick
                    ChatUser targetUser = _repository.VerifyUser(userName);

                    // Make sure the current user and target user are the same
                    if (user != targetUser)
                    {
                        throw new InvalidOperationException("You can't set/change the password for a nickname you down own.");
                    }

                    if (String.IsNullOrEmpty(newPassword))
                    {                        
                        if (targetUser.HashedPassword == null)
                        {
                            _chatService.SetUserPassword(user, password);

                            _notificationService.SetPassword();
                        }
                        else
                        {
                            throw new InvalidOperationException("Use /nick [nickname] [oldpassword] [newpassword] to change and existing password.");
                        }
                    }
                    else
                    {
                        _chatService.ChangeUserPassword(user, password, newPassword);

                        _notificationService.ChangePassword();
                    }
                }
            }

            // Commit the changes
            _repository.CommitChanges();
        }

        private void HandleNudge(ChatUser user, string[] parts)
        {
            if (_repository.Users.Count() == 1)
            {
                throw new InvalidOperationException("You're the only person in here...");
            }

            var toUserName = NormalizeUserName(parts[1]);

            ChatUser toUser = _repository.VerifyUser(toUserName);

            if (toUser == user)
            {
                throw new InvalidOperationException("You can't nudge yourself!");
            }

            string messageText = String.Format("{0} nudged you", user);

            var betweenNudges = TimeSpan.FromSeconds(60);
            if (toUser.LastNudged.HasValue && toUser.LastNudged > DateTime.Now.Subtract(betweenNudges))
            {
                throw new InvalidOperationException(String.Format("User can only be nudged once every {0} seconds", betweenNudges.TotalSeconds));
            }

            toUser.LastNudged = DateTime.Now;
            _repository.CommitChanges();

            _notificationService.NugeUser(user, toUser);
        }

        private void HandleNudge(ChatRoom room, ChatUser user, string[] parts)
        {
            var betweenNudges = TimeSpan.FromMinutes(1);
            if (room.LastNudged == null || room.LastNudged < DateTime.Now.Subtract(betweenNudges))
            {
                room.LastNudged = DateTime.Now;
                _repository.CommitChanges();

                _notificationService.NudgeRoom(room, user);
            }
            else
            {
                throw new InvalidOperationException(String.Format("Room can only be nudged once every {0} seconds", betweenNudges.TotalSeconds));
            }
        }

        private string NormalizeUserName(string userName)
        {
            return userName.StartsWith("@") ? userName.Substring(1) : userName;
        }
    }
}