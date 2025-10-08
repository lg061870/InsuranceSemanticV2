using System;
using System.Collections.Generic;
#nullable enable

namespace ConversaCore.Context
{
    /// <summary>
    /// Represents a user profile with common properties.
    /// </summary>
    public class UserProfile
    {
        /// <summary>
        /// Gets or sets whether the user has given consent.
        /// </summary>
        public bool ConsentGiven { get; set; }
        
        /// <summary>
        /// Gets or sets the user's name.
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Gets or sets the user's email.
        /// </summary>
        public string? Email { get; set; }
        
        /// <summary>
        /// Gets or sets additional properties.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Initializes the profile with default values.
        /// </summary>
        public void InitializeProfile()
        {
            ConsentGiven = false;
            Name = null;
            Email = null;
            Properties.Clear();
        }
    }
    
    /// <summary>
    /// Extension methods for IConversationContext to handle UserProfile.
    /// </summary>
    public static class ConversationContextExtensions
    {
        private const string UserProfileKey = "UserProfile";
        
        /// <summary>
        /// Gets the user profile from the conversation context.
        /// </summary>
        /// <param name="context">The conversation context.</param>
        /// <returns>The user profile.</returns>
        public static UserProfile GetUserProfile(this IConversationContext context)
        {
            if (!context.TryGetValue<UserProfile>(UserProfileKey, out var profile))
            {
                profile = new UserProfile();
                context.SetValue(UserProfileKey, profile);
            }
            return profile;
        }
        
        /// <summary>
        /// Sets the user profile in the conversation context.
        /// </summary>
        /// <param name="context">The conversation context.</param>
        /// <param name="profile">The user profile.</param>
        public static void SetUserProfile(this IConversationContext context, UserProfile profile)
        {
            context.SetValue(UserProfileKey, profile);
        }
    }
}