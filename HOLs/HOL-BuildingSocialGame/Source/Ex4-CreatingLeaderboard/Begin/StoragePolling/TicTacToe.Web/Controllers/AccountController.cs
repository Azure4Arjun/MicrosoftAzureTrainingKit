﻿using System;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using Microsoft.IdentityModel.Claims;
using Microsoft.IdentityModel.Protocols.WSFederation;
using Microsoft.Samples.SocialGames;
using Microsoft.Samples.SocialGames.Entities;
using Microsoft.Samples.SocialGames.Repositories;
using Microsoft.Samples.SocialGames.Web.Services;

namespace TicTacToe.Web.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IUserRepository userRepository;
        private IUserProvider userProvider;

        public AccountController(IUserRepository userRepository, IUserProvider userProvider)
        {
            this.userRepository = userRepository;
            this.userProvider = userProvider;
        }

        public ActionResult LogOn(string returnUrl)
        {
            return View();
        }

        [HttpPost]
        public ActionResult LogOn()
        {
            if (this.Request.IsAuthenticated)
            {
                // Ensure user profile
                var userId = this.GetClaimValue(ClaimTypes.NameIdentifier);
                var userProfile = this.userRepository.GetUser(userId);
                if (userProfile == null)
                {
                    var loginType = this.GetClaimValue(ConfigurationConstants.IdentityProviderClaimType);
                    userProfile = new UserProfile
                    {
                        Id = userId,
                        DisplayName = Thread.CurrentPrincipal.Identity.Name ?? string.Empty,
                        LoginType = loginType,
                        AssociatedUserAccount =
                            loginType.StartsWith("Facebook", StringComparison.OrdinalIgnoreCase) ?
                                this.GetClaimValue(ConfigurationConstants.FacebookAccessTokenClaimType) :
                                string.Empty
                    };

                    this.userRepository.AddOrUpdateUser(userProfile);
                }

                var effectiveReturnUrl = this.GetContextFromRequest();

                if (!string.IsNullOrWhiteSpace(effectiveReturnUrl))
                {
                    return Redirect(effectiveReturnUrl);
                }
            }

            return Redirect("~/");
        }

        [Authorize]
        public ActionResult Friends(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                string currentUserId = this.userProvider.UserId;
                string inviteUserId = id;

                this.userRepository.AddFriend(currentUserId, inviteUserId);
                this.userRepository.AddFriend(inviteUserId, currentUserId);
                return RedirectToAction("Index", "Home");
            }

            this.ViewBag.CurrentUserId = this.userProvider.UserId;
            return View();
        }

        private string GetContextFromRequest()
        {
            Uri requestBaseUrl = WSFederationMessage.GetBaseUrl(this.Request.Url);
            var message = WSFederationMessage.CreateFromNameValueCollection(requestBaseUrl, this.Request.Form);

            return message != null ? message.Context : string.Empty;
        }

        private string GetClaimValue(string claimType)
        {
            if (!Thread.CurrentPrincipal.Identity.IsAuthenticated)
            {
                throw new InvalidOperationException("User is not authenticated");
            }

            var claimsIdentity = (IClaimsIdentity)Thread.CurrentPrincipal.Identity;

            if (!claimsIdentity.Claims.Any(c => c.ClaimType.Equals(claimType, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Claim not found: " + claimType);
            }

            return claimsIdentity.Claims.FirstOrDefault(c => c.ClaimType.Equals(claimType, StringComparison.OrdinalIgnoreCase)).Value;
        }
    }
}
