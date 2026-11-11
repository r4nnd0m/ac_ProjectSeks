using System;
using System.Collections.Generic;
using System.Text;

namespace NewsApi.Models;

public record NewsItem(
    string Title,
    string Link,
    string Source,
    DateTimeOffset PublishedAT);