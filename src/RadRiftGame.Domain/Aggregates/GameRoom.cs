﻿using System;
using RadRiftGame.Contracts.Events;
using RadRiftGame.Contracts.ValueObjects;
using RadRiftGame.Domain.Services;
using RadRiftGame.Infrastructure;

namespace RadRiftGame.Domain.Aggregates
{
    public enum GameStatus
    {
        None = 0,
        Created = 1,
        Started = 2,
        Stoped = 3
    }
    public class GameRoom : AggregateRoot
    {
        private string _name;
        private GameRoomId _id;
        private UserId _host;
        private UserId _player1;
        private decimal _hostHealth;
        private decimal _player1Health;
        private GameStatus _status;

        public void Apply(GameRoomCreated e)
        {
            this._id = e.Id;
            this._name = e.Name;
            this._host = e.SysInfo.UserId;
            this._hostHealth = 10;
            this._status = GameStatus.Created;
        }
        
        public void Apply(GameStopped e)
        {
            this._status = GameStatus.Stoped;
        }

        public void Apply(UserJoinedGameRoom e)
        {
            this._player1 = e.UserId;
            this._player1Health = 10;
            this._status = GameStatus.Started;
        }

        public void Apply(UserHealsDecreased e)
        {
            if (e.UserId.Equals(this._player1))
                this._player1Health -= e.HealthDecrease;
            if (e.UserId.Equals(this._host))
                this._hostHealth -= e.HealthDecrease;
        }

        public void EnterUser(SysInfo sysInfo, GameRoomId gameRoomId, UserId userId)
        {
            if (this._host != userId && this._player1 == null)
            {
                this.ApplyChange(new UserJoinedGameRoom(sysInfo, gameRoomId, userId));
            }
        }

        public void DecreaseHealth(SysInfo sysInfo, GameRoomId gameRoomId, UserId userId, int health)
        {
            if (userId.Equals(this._player1) && this._player1Health <= 0)
                return;
            if (userId.Equals(this._host) && this._hostHealth <= 0)
                return;

            this.ApplyChange(new UserHealsDecreased(sysInfo, gameRoomId, userId, health));
            
            if (userId.Equals(this._player1) && this._player1Health-health <=0)
                this.ApplyChange(new GameStopped(sysInfo, gameRoomId, userId));
            if (userId.Equals(this._host) && this._hostHealth -health<= 0)
                this.ApplyChange(new GameStopped(sysInfo, gameRoomId, userId));
            
            
        }

        public override Guid Id
        {
            get { return _id.Id; }
        }

        public GameRoom()
        {
            // used to create in repository ... many ways to avoid this, eg making private constructor
        }

        public GameRoom(GameRoomId id, string name, UserId userId)
        {
            ApplyChange(new GameRoomCreated(id, name, SysInfo.CreateSysInfo(userId)));
        }

        public override string ToString()
        {
            var str = $"Game: {this._id.Id}, HostHealth: {this._hostHealth}, player1Health:{this._player1Health}, status:{this._status}";
            return str;
        }
    }
}