using System;
using System.Collections.Generic;
using System.Text;

namespace NewsApi.Dtos;

public record NewsItemResponse(
    string Title,
    string Link,
    string Source,
    DateTimeOffset PublishedAT
    );
