// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;

namespace IdentityServerHost.Quickstart.UI
{
    public class AccountOptions
    {
        public static bool AllowLocalLogin { get; set; } = true;
        public static bool AllowRememberLogin { get; set; } = true;
        public static TimeSpan RememberMeLoginDuration { get; set; } = TimeSpan.FromDays(30);

        public static bool ShowLogoutPrompt { get; set; } = true;
        public static bool AutomaticRedirectAfterSignOut { get; set; }

        public static string InvalidCredentialsErrorMessage { get; set; } = "Invalid username or password";
    }
}
