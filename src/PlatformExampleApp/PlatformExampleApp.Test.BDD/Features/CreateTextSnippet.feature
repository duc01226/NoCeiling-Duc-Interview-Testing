Feature: Create Text Snippet Feature

Scenario: Create a new random unique snippet text item should be successful
    Given Loaded success home page
    When Fill in a new random unique value snippet text item data (snippet text and full text) and submit a new text snippet item, wait for submit request finished
    Then Current page has no errors
    And Do search text snippet item with the snippet text that has just being created success must found exact one match item in the table for the search text
    And The item data should equal to the filled data when submit creating new text snippet item

Scenario: Create two new duplicated snippet text item should be failed
    Given Loaded success home page
    When Create a new random unique snippet text item successful and try create the same previous snippet text item value again
    Then Page must show create duplicated snippet text errors
