using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Mapping;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities;
using Microsoft.SqlServer.Server;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Sini
{
    internal class Auction : IAuction
    {
        public int Id { get; }
        public IUser Seller { get; }
        public string Description { get; }
        public DateTime EndsOn { get; }
        public double Price { get; set; }
        public ISite Site { get; }
        private readonly string _connectionString;
        private readonly IAlarmClock _alarmClock;
        private double maxOffer = 0;

        private enum Status
        {
            New,
            Changed
        }

        private Status CurrentBid;

        public Auction(int id, IUser seller, string description, DateTime endsOn, double price, ISite site, string connectionString, IAlarmClock alarmClock)
        {
            Id = id;
            Seller = seller;
            Description = description;
            EndsOn = endsOn;
            Price = price;
            Site = site;
            _connectionString = connectionString;
            _alarmClock = alarmClock;
        }

        protected bool Equals(Auction other)
        {
            return Id == other.Id && Equals(Seller, other.Seller) && string.Equals(Description, other.Description);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Auction)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id;
                hashCode = (hashCode * 397) ^ (Seller != null ? Seller.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Description != null ? Description.GetHashCode() : 0);
                return hashCode;
            }
        }

        public IUser CurrentWinner()
        {
            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                var auction = site.Auctions.SingleOrDefault(a => a.Id == Id);
                if (auction.Winner is null)
                    return null;
                return new User(auction.Winner.Username, Site, _connectionString, _alarmClock);
            }
        }

        public double CurrentPrice()
        {
            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                var auction = site.Auctions.SingleOrDefault(a => a.Id == Id);
                if (auction is null)
                    throw new InvalidOperationException();
                return auction.CurrentPrice;
            }
        }

        public void Delete()
        {
            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                var auction = context.Auctions.Find(Id);
                if (auction is null)
                    throw new InvalidOperationException();
                context.Auctions.Remove(auction);
                context.SaveChanges();
            }
        }

        public bool BidOnAuction(ISession session, double offer)
        {
            if (session is null)
                throw new ArgumentNullException();
            if (offer < 0)
                throw new ArgumentOutOfRangeException();
            if (EndsOn < _alarmClock.Now)
                throw new InvalidOperationException();
            using (var context = new SiteContext(_connectionString))
            {
                var site = context.Sites.SingleOrDefault(a => a.SiteName == Site.Name);
                if (site is null)
                    throw new InvalidOperationException();
                if (Seller.Equals(session.User))
                    throw new ArgumentException();
                if (!session.IsValid())
                    throw new ArgumentException();

                var user = site.Users.SingleOrDefault(a => a.Username == session.User.Username);
                if (user is null)
                    throw new InvalidOperationException();
                var auction = site.Auctions.SingleOrDefault(a => a.Id == Id);
                if (auction is null)
                    throw new InvalidOperationException();
                (session as Session)?.Update(_alarmClock.Now.AddSeconds(site.SessionExpirationTimeInSeconds));

                if (CurrentBid == Status.New)
                {
                    if (offer < Price)
                        return false;
                    CurrentBid = Status.Changed;
                    maxOffer = offer;
                    auction.Winner = user;
                    context.SaveChanges();
                    return true;
                }

                if (auction.Winner.Equals(user))
                {
                    if (offer <= maxOffer + site.MinimumBidIncrement)
                        return false;
                    maxOffer = offer;
                    context.SaveChanges();
                    return true;
                }

                if (offer < auction.CurrentPrice || offer < auction.CurrentPrice + site.MinimumBidIncrement)
                    return false;
                if (offer > maxOffer)
                {
                    Price = auction.CurrentPrice = Math.Min(offer, maxOffer + site.MinimumBidIncrement);
                    maxOffer = offer;
                    auction.Winner = user;
                    context.SaveChanges();
                    return true;
                }

                if (maxOffer > offer)
                {
                    Price = auction.CurrentPrice = Math.Min(maxOffer, offer + site.MinimumBidIncrement);
                    context.SaveChanges();
                    return true;
                }
                return false;
            }
        }
    }
}