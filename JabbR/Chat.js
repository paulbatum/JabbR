﻿/// <reference path="Scripts/jquery-1.7.js" />
/// <reference path="Scripts/jQuery.tmpl.js" />
/// <reference path="Scripts/jquery.cookie.js" />
/// <reference path="Chat.ui.js" />

(function ($, connection, window, ui) {
    "use strict";

    var chat = connection.chat,
        messageHistory = [],
        historyLocation = 0,
        originalTitle = document.title,
        unread = 0,
        focus = true,
        typingTimeoutId = null;

    function isSelf(user) {
        return chat.name === user.Name;
    }

    function populateRoom(room) {
        var d = $.Deferred();
        // Populate the list of users rooms and messages 
        chat.getRoomInfo(room)
                .done(function (roomInfo) {
                    $.each(roomInfo.Users, function () {
                        var viewModel = {
                            name: this.Name,
                            hash: this.Hash
                        };

                        ui.addUser(viewModel, room);
                        ui.setUserActivity(this);
                    });

                    $.each(roomInfo.Owners, function () {
                        ui.setRoomOwner(this, room);
                    });

                    $.each(roomInfo.RecentMessages, function () {
                        var viewModel = getMessageViewModel(this);

                        ui.addChatMessage(viewModel, room);
                    });

                    ui.scrollToBottom(room);

                    d.resolveWith(chat);
                })
                .fail(function () {
                    d.rejectWith(chat);
                });

        return d;
    }

    function scrollIfNecessary(callback, room) {
        var nearEnd = ui.isNearTheEnd(room);

        callback();

        if (nearEnd) {
            ui.scrollToBottom(room);
        }
    }

    function getMessageViewModel(message) {
        var re = new RegExp("\\b@?" + chat.name.replace(/\./, '\\.') + "\\b", "i");
        return {
            name: message.User.Name,
            hash: message.User.Hash,
            message: message.Content,
            id: message.Id,
            date: message.When.fromJsonDate(),
            highlight: re.test(message.Content) ? 'highlight' : ''
        };
    }

    // Save some state in a cookie
    function updateCookie() {
        var legacyCookies = ['userid', 'username', 'userroom', 'userhash', 'currentroom'],
            state = {
                userId: chat.id,
                activeRoom: chat.activeRoom
            },
            jsonState = window.JSON.stringify(state);

        // Clear the legacy cookies
        $.each(legacyCookies, function () {
            $.cookie(this, null);
        });

        $.cookie('jabbr.state', jsonState, { path: '/', expires: 30 });
    }

    function updateTitle() {
        if (unread === 0) {
            document.title = originalTitle;
        }
        else {
            document.title = '(' + unread + ') ' + originalTitle;
        }
    }

    function updateUnread(room) {
        if (focus === false) {
            unread = unread + 1;
        }

        ui.updateUnread(room);

        updateTitle();
    }

    // Room commands

    // When the /join command gets raised this is called
    chat.joinRoom = function (room) {
        ui.addRoom(room);
        ui.setActiveRoom(room);

        populateRoom(room).done(function () {
            ui.addMessage('You just entered ' + room, 'notification', room);
        });
    };

    // Called when a returning users join chat
    chat.initialize = function (rooms) {
        $.each(rooms, function (index, room) {
            ui.addRoom(room);
            populateRoom(room);
        });

        var activeRoom = this.activeRoom;
        ui.addMessage('Welcome back ' + chat.name, 'notification', 'lobby');
        ui.addMessage('You can join any of the rooms on the right', 'notification', 'lobby');

        // Process any urls that may contain room names
        ui.run();

        // If the active room didn't change then set the active room (since no navigation happened)
        if (activeRoom === this.activeRoom) {
            ui.setActiveRoom(this.activeRoom || 'Lobby');
        }
    };

    chat.addOwner = function (user, room) {
        ui.setRoomOwner(user.Name, room);
    };

    chat.updateRoomCount = function (room, count) {
        ui.updateLobbyRoomCount(room, count);
    };

    chat.markInactive = function (users) {
        $.each(users, function () {
            ui.setUserActivity(this);
        });
    };

    chat.updateActivity = function (user) {
        ui.setUserActivity(user);
    };

    chat.addMessageContent = function (id, content, room) {
        var nearTheEndBefore = ui.isNearTheEnd(room);

        scrollIfNecessary(function () {
            ui.addChatMessageContent(id, content, room);
        }, room);

        updateUnread(room);

        // Adding external content can sometimes take a while to load
        // Since we don't know when it'll become full size in the DOM
        // we're just going to wait a little bit and hope for the best :) (still a HACK tho)
        window.setTimeout(function () {
            var nearTheEndAfter = ui.isNearTheEnd(room);
            ui.resize();
            if (nearTheEndBefore && nearTheEndAfter) {
                ui.scrollToBottom();
            }
        }, 850);
    };

    chat.addMessage = function (message, room) {
        scrollIfNecessary(function () {
            var viewModel = getMessageViewModel(message);
            ui.addChatMessage(viewModel, room);

        }, room);

        updateUnread(room);
    };

    chat.addUser = function (user, room, isOwner) {
        var viewModel = {
            name: user.Name,
            hash: user.Hash,
            owner: isOwner
        };

        var added = ui.addUser(viewModel, room);

        if (added) {
            if (!isSelf(user)) {
                ui.addMessage(user.Name + ' just entered ' + room, 'notification', room);
            }
        }
    };

    chat.changeUserName = function (oldName, user, room) {
        ui.changeUserName(oldName, user, room);

        if (!isSelf(user)) {
            ui.addMessage(oldName + '\'s nick has changed to ' + user.Name, 'notification', room);
        }
    };

    chat.changeGravatar = function (user, room) {
        ui.changeGravatar(user, room);

        if (!isSelf(user)) {
            ui.addMessage(user.Name + "'s gravatar changed.", 'notification', room);
        }
    };

    // User single client commands

    // Called when you make someone an owner
    chat.ownerMade = function (user, room) {
        ui.addMessage(user + ' is now an owner of ' + room, 'notification', this.activeRoom);
    };

    // Called when you've been made an owner
    chat.makeOwner = function (room) {
        ui.addMessage('You are now an owner of ' + room, 'notification', this.activeRoom);
    };

    // Called when your gravatar has been changed
    chat.gravatarChanged = function () {
        ui.addMessage('Your gravatar has been set', 'notification', this.activeRoom);
    };

    // Called when you created a new user
    chat.userCreated = function () {
        ui.addMessage('Your nick is ' + this.name, 'notification');

        // Process any urls that may contain room names
        ui.run();

        if (!this.activeRoom) {
            // Set the active room to the lobby so the rooms on the right load
            ui.setActiveRoom('Lobby');
        }

        // Update the cookie
        updateCookie();
    };

    chat.logOut = function (rooms) {
        ui.setActiveRoom('Lobby');

        // Close all rooms
        $.each(rooms, function () {
            ui.removeRoom(this);
        });

        ui.addMessage("You've been logged out.", 'notification', this.activeRoom);

        chat.activeRoom = undefined;
        chat.name = undefined;
        chat.id = undefined;

        updateCookie();

        // Restart the connection
        connection.stop();
        connection.start();
    };

    chat.setPassword = function () {
        ui.addMessage('Your password has been set', 'notification', this.activeRoom);
    };

    chat.changePassword = function () {
        ui.addMessage('Your password has been changed', 'notification', this.activeRoom);
    };

    chat.userNameChanged = function (user) {
        ui.addMessage('Your name is now ' + user.Name, 'notification', this.activeRoom);
    };

    chat.setTyping = function (user, room, isTyping) {
        ui.setUserTyping(user, room, isTyping);
    };

    chat.sendMeMessage = function (name, message) {
        ui.addMessage('*' + name + ' ' + message, 'notification');
    };

    chat.sendPrivateMessage = function (from, to, message) {
        ui.addMessage('<emp>*' + from + '* &raquo; *' + to + '*</emp> ' + message, 'pm');
    };

    chat.nudge = function (from, to) {
        function shake(n) {
            var move = function (x, y) {
                parent.moveBy(x, y);
            };
            for (var i = n; i > 0; i--) {
                for (var j = 1; j > 0; j--) {
                    move(i, 0);
                    move(0, -i);
                    move(-i, 0);
                    move(0, i);
                    move(i, 0);
                    move(0, -i);
                    move(-i, 0);
                    move(0, i);
                    move(i, 0);
                    move(0, -i);
                    move(-i, 0);
                    move(0, i);
                }
            }
            return this;
        };
        $("body").effect("pulsate", { times: 3 }, 300);
        window.setTimeout(function () {
            shake(20);
        }, 300);

        ui.addMessage('*' + from + ' nudged ' + (to ? 'you' : 'the room'), to ? 'pm' : 'notification');
    };

    chat.leave = function (user, room) {
        if (isSelf(user)) {
            ui.setActiveRoom('Lobby');
            ui.removeRoom(room);
            ui.addMessage('You have left ' + room, 'notification');
        }
        else {
            ui.removeUser(user, room);
            ui.addMessage(user.Name + ' left ' + room, 'notification', room);
        }
    };

    chat.kick = function (room) {
        ui.setActiveRoom('Lobby');
        ui.removeRoom(room);
        ui.addMessage('You were kicked from ' + room, 'notification');
    };

    // Helpish commands
    chat.showRooms = function (rooms) {
        ui.addMessage('<h3>Rooms</h3>');
        if (!rooms.length) {
            ui.addMessage('No rooms available', 'notification');
        }
        else {
            $.each(rooms, function () {
                ui.addMessage(this.Name + ' (' + this.Count + ')');
            });
        }
        ui.addMessage('<br/>');
    };

    chat.showCommands = function (commands) {
        ui.addMessage('<h3>Help</h3>');
        $.each(commands, function () {
            ui.addMessage(this.Name + ' - ' + this.Description);
        });
        ui.addMessage('<br />');
    };

    chat.showUsersInRoom = function (room, names) {
        ui.addMessage('<h3> Users in ' + room + '</h3>');
        if (names.length === 0) {
            ui.addMessage('Room is empty');
        }
        else {
            $.each(names, function () {
                ui.addMessage('- ' + this);
            });
        }
    };

    chat.listUsers = function (users) {
        if (users.length === 0) {
            ui.addMessage('<h3>No users matched your search</h3>');
        }
        else {
            ui.addMessage('<h3> The following users match your search </h3>');
            ui.addMessage(users.join(', '));
        }
    };

    chat.showUsersRoomList = function (user, rooms) {
        if (rooms.length == 0) {
            ui.addMessage('<h3>' + user + ' is not in any rooms</h3>');
        }
        else {
            ui.addMessage('<h3>' + user + ' is in the following rooms</h3>');
            ui.addMessage(rooms.join(', '));
        }
    };

    $(ui).bind('ui.typing', function () {
        // If not in a room, don't try to send typing notifications
        if (!chat.activeRoom) {
            return;
        }

        // Clear any previous timeout
        if (typingTimeoutId) {
            clearTimeout(typingTimeoutId);
        }
        else {
            // Otherwise, mark as typing
            chat.typing(true);
        }

        // Set timeout to turn off
        typingTimeoutId = window.setTimeout(function () {
            typingTimeoutId = 0;
            chat.typing(false);
        }, 3000);
    });

    $(ui).bind('ui.sendMessage', function (ev, msg) {
        chat.send(msg)
            .fail(function (e) {
                ui.addMessage(e, 'error');
            });

        clearTimeout(typingTimeoutId);
        typingTimeoutId = 0;
        chat.typing(false);

        // Store message history
        messageHistory.push(msg);

        // REVIEW: should this pop items off the top after a certain length?
        historyLocation = messageHistory.length;
    });

    $(ui).bind('ui.focus', function () {
        focus = true;
        unread = 0;
        updateTitle();
    });

    $(ui).bind('ui.blur', function () {
        focus = false;

        updateTitle();
    });

    $(ui).bind('ui.openRoom', function (ev, room) {
        chat.send('/join ' + room)
            .fail(function (e) {
                ui.setActiveRoom('Lobby');
                ui.addMessage(e, 'error');
            });
    });

    $(ui).bind('ui.closeRoom', function (ev, room) {
        chat.send('/leave ' + room)
            .fail(function (e) {
                ui.addMessage(e, 'error');
            });
    });

    $(ui).bind('ui.prevMessage', function () {
        historyLocation -= 1;
        if (historyLocation < 0) {
            historyLocation = messageHistory.length - 1;
        }
        ui.setMessage(messageHistory[historyLocation]);
    });

    $(ui).bind('ui.nextMessage', function () {
        historyLocation = (historyLocation + 1) % messageHistory.length;
        ui.setMessage(messageHistory[historyLocation]);
    });

    $(ui).bind('ui.activeRoomChanged', function (ev, room) {
        if (room === 'Lobby') {
            // Populate the user list with room names
            chat.getRooms()
                .done(function (rooms) {
                    ui.populateLobbyRooms(rooms);
                });

            // Remove the active room
            chat.activeRoom = undefined;
        }
        else {
            // When the active room changes update the client state and the cookie
            chat.activeRoom = room;
        }

        ui.scrollToBottom(room);
        updateCookie();
    });

    $(function () {
        // Initialize the ui
        ui.initialize();

        ui.addMessage('Welcome to the ' + originalTitle, 'notification');
        ui.addMessage('Type /help to see the list of commands', 'notification');

        connection.hub.start(function () {
            chat.join()
                .fail(function (e) {
                    ui.addMessage(e, 'error');
                })
                .done(function (success) {
                    if (success === false) {
                        ui.addMessage('Choose a name using "/nick nickname".', 'notification');
                    }
                });
        });
    });

})(jQuery, $.connection, window, window.chat.ui);