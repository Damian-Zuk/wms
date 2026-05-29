using System;
using System.Collections.Generic;
using System.Text;

namespace Wms.Application.Common.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(
        string userId,
        string userName,
        string email,
        string firstName,
        string lastName,
        IList<string> roles);
}