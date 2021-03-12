using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Entities;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Sini
{
    public class User : IUser
    {
        public string Username { get; }
        public ISite Site { get; }
        private readonly string _connectionString;
        private readonly IAlarmClock _alarmClock;

        public User(string username, ISite site, string connectionString, IAlarmClock alarmClock)
        {
            Username = username;
            Site = site;
            _connectionString = connectionString;
            _alarmClock = alarmClock;
        }

        protected bool Equals(User other)
        {
            return string.Equals(_connectionString, other._connectionString) && string.Equals(Username, other.Username) && Equals(Site, other.Site);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((User)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_connectionString != null ? _connectionString.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Username != null ? Username.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Site != null ? Site.GetHashCode() : 0);
                return hashCode;
            }
        }

        public IEnumerable<IAuction> WonAuctions()
        {
            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                var user = site.Users.SingleOrDefault(a => a.Username == Username);
                if (user is null)
                    throw new InvalidOperationException();
                List<IAuction> WonAuction = new List<IAuction>();
                foreach (var curr in site.Auctions.Where(a => a.Winner.Username == Username))
                {
                    WonAuction.Add(new Auction(curr.Id,
                        new User(curr.Seller.Username, Site, _connectionString, _alarmClock),
                        curr.Description, curr.EndsOn, curr.CurrentPrice, Site, _connectionString, _alarmClock));
                }

                return WonAuction;
            }
        }

        public void Delete()
        {
            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                var user = site.Users.SingleOrDefault(a => a.Username.Equals(Username));
                if (user is null)
                    throw new InvalidOperationException();
                if (site.Auctions.Any(a => a.Seller == user && a.EndsOn >= _alarmClock.Now))
                    throw new InvalidOperationException();
                if (site.Auctions.Any(b => b.Winner == user && b.EndsOn >= _alarmClock.Now))
                    throw new InvalidOperationException();
                var session = site.Sessions.Where(a => a.User.Username == Username).ToList();
                session.Clear();
                context.Users.Remove(user);
                context.SaveChanges();
            }
        }
    }
}
