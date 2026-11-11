using System;
using System.Collections.Generic;
using System.Text;

namespace AuthApi.Services;
public enum DeactivateOutcome
{
    Ok,
    NotFound,
    CannotDeactivateSelf
}