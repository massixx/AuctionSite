using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Sini
{
    public class Session : ISession

    {
        private readonly string _connectionString;
        private readonly IAlarmClock _alarmClock;
        public string Id { get; }
        public DateTime ValidUntil { get; set; }
        public IUser User { get; }
        public ISite Site { get; }

        public Session(string connectionString, IAlarmClock alarmClock, string id, DateTime validUntil, IUser user, ISite site)
        {
            _connectionString = connectionString;
            _alarmClock = alarmClock;
            Id = id;
            ValidUntil = validUntil;
            User = user;
            Site = site;
        }

        protected bool Equals(Session other)
        {
            return string.Equals(_connectionString, other._connectionString) && Equals(_alarmClock, other._alarmClock) && string.Equals(Id, other.Id) && Equals(User, other.User) && Equals(Site, other.Site);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Session)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_connectionString != null ? _connectionString.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (_alarmClock != null ? _alarmClock.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Id != null ? Id.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (User != null ? User.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Site != null ? Site.GetHashCode() : 0);
                return hashCode;
            }
        }

        public bool IsValid()
        {
            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                var session = site.Sessions.SingleOrDefault(a => a.Id == Id);
                return !(session is null) && _alarmClock.Now < session.ValidUntil && _alarmClock.Now < ValidUntil;
            }
        }

        public void Logout()
        {
            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                var session = site.Sessions.SingleOrDefault(a => a.Id == Id);
                if (session is null || session.ValidUntil < _alarmClock.Now)
                    throw new InvalidOperationException();
                session.ValidUntil = session.ValidUntil.AddMinutes(-9000);
                context.SaveChanges();
            }

        }

        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            if (description is null)
                throw new ArgumentNullException();
            if (description == "")
                throw new ArgumentException();
            if (startingPrice < 0)
                throw new ArgumentOutOfRangeException();
            if (endsOn < _alarmClock.Now)
                throw new UnavailableTimeMachineException();
            if (!this.IsValid())
                throw new InvalidOperationException();

            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                var session = context.Sessions.Find(Id);
                if (session is null)
                    throw new InvalidOperationException();
                var tupla = new AuctionDb()
                {
                    Seller = session.User,
                    Description = description,
                    EndsOn = endsOn,
                    SiteName = Site.Name,
                    CurrentPrice = startingPrice
                };
                context.Auctions.Add(tupla);
                context.SaveChanges();
                ValidUntil = _alarmClock.Now.AddSeconds(Site.SessionExpirationInSeconds);
                session.ValidUntil = ValidUntil;
                return new Auction(tupla.Id, User, description, endsOn, startingPrice, Site, _connectionString, _alarmClock);
            }
        }

        internal void Update(DateTime newValidUntil)
        {
            ValidUntil = newValidUntil;
        }
    }
}