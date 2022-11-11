Utils is place to store "common" functions which any project could use it.

Utils default grouping by "output" and (many inputs with no inputs could be treat as a main input), either by the output data type, or serve a "functional purpose".
Example:
    - Utils.String should produce string as output.
    - Utils.FullTextSearchChecker should have functions related to do FullTextSearch.
    - Utils.Values should only do the functional related to "values of an object" like CopyValues, HasDifferentValues
