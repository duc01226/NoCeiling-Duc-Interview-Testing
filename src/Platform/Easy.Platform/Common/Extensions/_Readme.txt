Extensions is place to store "common" functions which any project could use it.

Extensions default grouping by working with first "input" type as a main input, extend the functionality for that type. The type must have knowledge about the output.

OR functionality which existing both Output is the main for extension, but in the param existing the main input for the output too.
Ex: UrlExtension : Uri ToUri(this string url). Uri is output for extension,
but the string url could be treated as the main input for the output.

OR naming extension group by functionality explicitly, because both extension type and output type could not be the main one to group, but the functionality grouping is correctly.
Ex: EnsureThrowCommonExceptionExtension
