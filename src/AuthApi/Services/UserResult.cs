using AuthApi.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuthApi.Services;
public record UserResult(
    bool Succeeded,
    UserResponse? User,
    string[] Errors);




