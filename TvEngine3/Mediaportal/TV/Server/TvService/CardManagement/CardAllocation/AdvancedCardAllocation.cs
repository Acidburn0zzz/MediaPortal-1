#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

#region usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mediaportal.TV.Server.TVControl;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVService.CardManagement.CardHandler;
using Mediaportal.TV.Server.TVService.Interfaces.CardHandler;
using Mediaportal.TV.Server.TVService.Interfaces.Enums;
using Mediaportal.TV.Server.TVService.Interfaces.Services;
using Mediaportal.TV.Server.TVService.Scheduler;
using Mediaportal.TV.Server.TVService.Services;

#endregion

namespace Mediaportal.TV.Server.TVService.CardManagement.CardAllocation
{
  public class AdvancedCardAllocation : CardAllocationBase, ICardAllocation
  {
    #region private members   

    public AdvancedCardAllocation()
      : base()
    {
    }

    private static bool IsCardEnabled(ITvCardHandler cardHandler)
    {
      return cardHandler.DataBaseCard.enabled;
    }

    private bool IsCardPresent(int cardId)
    {
      bool isCardPresent = false;
      try
      {
        if (ServiceManager.Instance.InternalControllerService.IsCardPresent(cardId))
        {
          isCardPresent = true;
        }
      }
      catch (Exception)
      {
        isCardPresent = true;
      }
      return isCardPresent;
    }

    protected virtual int NumberOfOtherUsersOnCurrentCard(ITvCardHandler card, IUser user)
    {
      //determine how many other users are using this card
      int nrOfOtherUsers = 0;
      IDictionary<string, IUser> users = card.UserManagement.Users;
      if (users.Count > 0)
      {
        nrOfOtherUsers = users.Count(t => t.Value.Name != user.Name && !t.Value.Name.Equals("epg"));
      }

      if (LogEnabled)
      {
        Log.Info("Controller:    card:{0} type:{1} users: {2}", card.DataBaseCard.idCard, card.Type, nrOfOtherUsers);
      }

      return nrOfOtherUsers;
    }

    #endregion

    #region ICardAllocation Members

    /// <summary>
    /// Gets a list of all free cards which can receive the channel specified
    /// List is sorted.
    /// </summary>
    /// <returns>list containg all free cards which can receive the channel</returns>
    public List<CardDetail> GetFreeCardsForChannel(IDictionary<int, ITvCardHandler> cards, Channel dbChannel,
                                                   ref IUser user)
     {
       TvResult result;
       return GetFreeCardsForChannel(cards, dbChannel, ref user, out result);
     }

    /// <summary>
    /// Gets a list of all free cards which can receive the channel specified
    /// List is sorted.
    /// </summary>
    /// <returns>list containg all free cards which can receive the channel</returns>
    public List<CardDetail> GetFreeCardsForChannel(IDictionary<int, ITvCardHandler> cards, Channel dbChannel,
                                                   ref IUser user, out TvResult result)
    {
      Stopwatch stopwatch = Stopwatch.StartNew();
      try
      {
        //Log.Info("GetFreeCardsForChannel st {0}", Environment.StackTrace);
        //construct list of all cards we can use to tune to the new channel
        if (LogEnabled)
        {
          Log.Info("Controller: find free card for channel {0}", dbChannel.displayName);
        }
        var cardsAvailable = new List<CardDetail>();

        IDictionary<int, TvResult> cardsUnAvailable;
        List<CardDetail> cardDetails = GetAvailableCardsForChannel(cards, dbChannel, ref user, out cardsUnAvailable);
        foreach (CardDetail cardDetail in cardDetails)
        {
          ITvCardHandler tvCardHandler = cards[cardDetail.Card.idCard];
          bool checkTransponder = CheckTransponder(user, tvCardHandler, cardDetail.TuningDetail);
          if (checkTransponder)
          {
            cardsAvailable.Add(cardDetail);
          }
        }

        //sort cards
        cardsAvailable.Sort();

        if (cardsAvailable.Count > 0)
        {
          result = TvResult.Succeeded;
        }
        else
        {
          TvResult resultNoCards = GetResultNoCards(cardsUnAvailable);
          result = cardDetails.Count == 0 ? resultNoCards : TvResult.AllCardsBusy;
        }
        if (LogEnabled)
        {
          Log.Info("Controller: found {0} free card(s)", cardsAvailable.Count);
        }

        return cardsAvailable;
      }
      catch (Exception ex)
      {
        result = TvResult.UnknownError;
        Log.Write(ex);
        return null;
      }
      finally
      {
        stopwatch.Stop();
        Log.Info("AdvancedCardAllocation.GetFreeCardsForChannel took {0} msec", stopwatch.ElapsedMilliseconds);
      }
    }

    private static TvResult GetResultNoCards(IDictionary<int, TvResult> cardsUnAvailable) 
    {
      TvResult resultNoCards = TvResult.ChannelNotMappedToAnyCard;
      //Dictionary<int, TvResult>.ValueCollection values = cardsUnAvailable.Values;
      ICollection<TvResult> values = cardsUnAvailable.Values;

      if (values.Any(tvResult => tvResult == TvResult.ChannelIsScrambled)) 
      {
        resultNoCards = TvResult.ChannelIsScrambled;
      }
      return resultNoCards;
    }

    /// <summary>
    /// Gets a list of all available cards which can receive the channel specified
    /// List is sorted.
    /// </summary>
    /// <returns>list containg all cards which can receive the channel</returns>
    public List<CardDetail> GetAvailableCardsForChannel(IDictionary<int, ITvCardHandler> cards, Channel dbChannel,
                                                        ref IUser user)
    {
      IDictionary<int, TvResult> cardsUnAvailable;
      return GetAvailableCardsForChannel(cards, dbChannel, ref user, out cardsUnAvailable);
    }

    /// <summary>
    /// Gets a list of all available cards which can receive the channel specified
    /// List is sorted.
    /// </summary>
    /// <returns>list containg all cards which can receive the channel</returns>
    public List<CardDetail> GetAvailableCardsForChannel(IDictionary<int, ITvCardHandler> cards, Channel dbChannel, ref IUser user, out IDictionary<int, TvResult> cardsUnAvailable)
    {      
      Stopwatch stopwatch = Stopwatch.StartNew();
      cardsUnAvailable = new Dictionary<int, TvResult>();
      try
      {
        //Log.Info("GetFreeCardsForChannel st {0}", Environment.StackTrace);
        //construct list of all cards we can use to tune to the new channel
        var cardsAvailable = new List<CardDetail>();        
        if (LogEnabled)
        {
          Log.Info("Controller: find card for channel {0}", dbChannel.displayName);
        }
        //get the tuning details for the channel
        ICollection<IChannel> tuningDetails = CardAllocationCache.GetTuningDetailsByChannelId(dbChannel);
        bool isValidTuningDetails = IsValidTuningDetails(tuningDetails);
        if (!isValidTuningDetails)
        {
          //no tuning details??
          if (LogEnabled)
          {
            Log.Info("Controller:  No tuning details for channel:{0}", dbChannel.displayName);
          }
          return cardsAvailable;
        }

        if (LogEnabled)
        {
          Log.Info("Controller:   got {0} tuning details for {1}", tuningDetails.Count, dbChannel.displayName);
        }
        int number = 0;
        ICollection<ITvCardHandler> cardHandlers = cards.Values;

        foreach (IChannel tuningDetail in tuningDetails)
        {
          cardsUnAvailable.Clear();
          number++;
          if (LogEnabled)
          {
            Log.Info("Controller:   channel #{0} {1} ", number, tuningDetail.ToString());
          }
          foreach (ITvCardHandler cardHandler in cardHandlers)
          {
            int cardId = cardHandler.DataBaseCard.idCard;

            if (cardsUnAvailable.ContainsKey(cardId))
            {
              if (LogEnabled)
              {
                Log.Info("Controller:    card:{0} has already been queried, skipping.", cardId);
              }
              continue;
            }
            if (!CanCardTuneChannel(cardHandler, dbChannel, tuningDetail))
            {
              AddCardUnAvailable(ref cardsUnAvailable, cardId, TvResult.ChannelNotMappedToAnyCard);
              continue;
            }

            if (!CanCardDecodeChannel(cardHandler, tuningDetail))
            {
              AddCardUnAvailable(ref cardsUnAvailable, cardId, TvResult.ChannelIsScrambled);
              continue;
            }

            //ok card could be used to tune to this channel
            bool isSameTransponder = IsSameTransponder(cardHandler, tuningDetail);
            if (LogEnabled)
            {
              Log.Info("Controller:    card:{0} type:{1} can tune to channel", cardId, cardHandler.Type);
            }
            int nrOfOtherUsers = NumberOfOtherUsersOnCurrentCard(cardHandler, user);
            //bool isParked = IsCardParkedOnOtherChannel(cardHandler, dbChannel.idChannel);
            var cardInfo = new CardDetail(cardId, cardHandler.DataBaseCard, tuningDetail, isSameTransponder,
                                                 nrOfOtherUsers);
            cardsAvailable.Add(cardInfo);
          }
        }


        //sort cards
        cardsAvailable.Sort();
        if (LogEnabled)
        {
          Log.Info("Controller: found {0} card(s) for channel", cardsAvailable.Count);
        }

        return cardsAvailable;
      }
      catch (Exception ex)
      {
        Log.Write(ex);
        return null;
      }
      finally
      {
        stopwatch.Stop();
        Log.Info("AdvancedCardAllocation.GetAvailableCardsForChannel took {0} msec", stopwatch.ElapsedMilliseconds);
      }
    }

    /*private bool IsCardParkedOnOtherChannel(ITvCardHandler cardHandler, int idChannel)
    {
      return cardHandler.IsCardParkedOnOtherChannel(idChannel);
    }*/

    private static bool CanCardDecodeChannel(ITvCardHandler cardHandler, IChannel tuningDetail)
    {
      bool canCardDecodeChannel = true;
      int cardId = cardHandler.DataBaseCard.idCard;
      if (!tuningDetail.FreeToAir && !cardHandler.DataBaseCard.CAM)
      {
        Log.Info("Controller:    card:{0} type:{1} channel is encrypted but card has no CAM", cardId, cardHandler.Type);
        canCardDecodeChannel = false;
      }
      return canCardDecodeChannel;
    }

    protected virtual bool CanCardTuneChannel(ITvCardHandler cardHandler, Channel dbChannel, IChannel tuningDetail)
    {
      int cardId = cardHandler.DataBaseCard.idCard;
      bool isCardEnabled = IsCardEnabled(cardHandler);
      if (!isCardEnabled)
      {
        //not enabled, so skip the card
        if (LogEnabled)
        {
          Log.Info("Controller:    card:{0} type:{1} is disabled", cardId, cardHandler.Type);
        }
        return false;
      }

      bool isCardPresent = IsCardPresent(cardId);
      if (!isCardPresent)
      {        
        return false;
      }

      if (!cardHandler.Tuner.CanTune(tuningDetail))
      {
        //card cannot tune to this channel, so skip it
        if (LogEnabled)
        {
          Log.Info("Controller:    card:{0} type:{1} cannot tune to channel", cardId, cardHandler.Type);
        }
        return false;
      }

      //check if channel is mapped to this card and that the mapping is not for "Epg Only"
      bool isChannelMappedToCard = CardAllocationCache.IsChannelMappedToCard(dbChannel, cardHandler.DataBaseCard);
      if (!isChannelMappedToCard)
      {
        if (LogEnabled)
        {
          Log.Info("Controller:    card:{0} type:{1} channel not mapped", cardId, cardHandler.Type);
        }
        return false;
      }
      return true;
    }

    private static void AddCardUnAvailable(ref IDictionary<int, TvResult> cardsUnAvailable, int cardId, TvResult tvResult)
    {
      if (!cardsUnAvailable.ContainsKey(cardId))
      {
        cardsUnAvailable.Add(cardId, tvResult);
      }
    }

    #endregion

    #region public members       

    #endregion
  }
}