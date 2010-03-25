using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;


namespace Nocs.Helpers
{
    public static class RegexHelpers
    {
        // reference for the main form so we can access its methods/properties
        private static readonly CultureInfo Culture = new CultureInfo("en-us");

        // holds the current character index
        private static int _currentIndex;


        // function for moving to the next search occurence, returns true if one is found
        public static bool FindNext(string searchString, bool caseSensitive, RichTextBox txtControl, bool useRegularExpression)
        {
            // local Regex variable used throughout the function
            Regex regularExpression;

            // get the length of the search string
            var searchLength = searchString.Length;


            #region Case Sensitive

            if (caseSensitive)
            {
                if (useRegularExpression)
                {
                    // we are using regular expressions, create a new Regex instance
                    try
                    {
                        regularExpression = new Regex(searchString);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Invalid Regular Expression!");
                        return false;
                    }

                    // expression is fine, let's try to match it starting from the current position
                    var match = regularExpression.Match(txtControl.Text, _currentIndex);

                    // if matched successfully, get the index location of the match inside the
                    // textbox control and the length of the match
                    if (match.Success)
                    {
                        _currentIndex = match.Index;
                        searchLength = match.Length;
                    }
                    else
                    {
                        // no match, reset position
                        _currentIndex = -1;
                    }
                }
                else
                {
                    // not a regular expression search, just match the literal string
                    _currentIndex = txtControl.Text.IndexOf(searchString, _currentIndex);
                }
            }

            #endregion

            #region Case-Insensivite

            else
            {
                if (useRegularExpression)
                {
                    try
                    {
                        // set the IgnoreCase and Multiline options for our regular expression
                        regularExpression = new Regex(searchString, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Invalid Regular expression!");
                        return false;
                    }

                    // expression fine, let's try to match it starting from the current position
                    var match = regularExpression.Match(txtControl.Text, _currentIndex);

                    // if matched successfully, get the index location of the match inside
                    // the textbox control and the length of the match
                    if (match.Success)
                    {
                        _currentIndex = match.Index;
                        searchLength = match.Length;
                    }
                    else
                    {
                        // no match, reset position
                        _currentIndex = -1;
                    }
                }
                else
                {
                    // this search is for basic, case-insensitive search
                    var culture = new CultureInfo("en-us");

                    _currentIndex = culture.CompareInfo.IndexOf(txtControl.Text, searchString, _currentIndex, CompareOptions.IgnoreCase);
                }
            }

            #endregion


            // if we found a match, select it in the textbox
            if (_currentIndex >= 0)
            {
                // select the matching text
                txtControl.SelectionStart = txtControl.Text.IndexOf("\n", _currentIndex) + 2;
                txtControl.SelectionLength = 0;

                txtControl.SelectionStart = _currentIndex;
                txtControl.SelectionLength = searchLength;
                _currentIndex += searchLength; // advance past selection
                txtControl.ScrollToCaret(); // scroll to selection
            }
            else
            {
                // no match, reached the end of the document
                MessageBox.Show("No matches, reached the end of the document.");
                _currentIndex = 0;
                return false;
            }

            return true;
        }

        // function for replacing the already found (and selected) occurrence
        public static void Replace(string searchString, string replaceString, bool caseSensitive, RichTextBox txtControl, bool useRegularExpression)
        {
            // make sure text is selected
            if (txtControl.SelectionLength > 0)
            {
                Regex regularExpression;

                if (caseSensitive)
                {
                    #region Case Sensitive & Regular Expression

                    if (useRegularExpression)
                    {
                        try
                        {
                            // create a new expression
                            regularExpression = new Regex(searchString);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Invalid Regular Expression!");
                            return;
                        }

                        // expression fine, match against current selection
                        var match = regularExpression.Match(txtControl.SelectedText);

                        if (match.Success)
                        {
                            // replace text and substract from index if chars were removed
                            replaceString = Regex.Replace(replaceString, @"\\n", "\n");
                            var replacedString = regularExpression.Replace(txtControl.SelectedText, replaceString);
                            txtControl.SelectedText = replacedString;
                            _currentIndex -= (match.Length - replacedString.Length);

                            // check for next occurrence
                            var match2 = regularExpression.Match(txtControl.Text, _currentIndex);

                            if (match2.Success)
                            {
                                _currentIndex = match2.Index;
                                // jump to next occurrence
                                FindNext(searchString, true, txtControl, true);
                            }
                            else
                            {
                                MessageBox.Show("No matches, reached the end of the document.");
                                _currentIndex = 0;
                                return;
                            }
                        }
                        else
                        {
                            MessageBox.Show("No matches, reached the end of the document.");
                            _currentIndex = 0;
                            return;
                        }
                    }

                    #endregion

                    #region Case Sensitive & Basic

                    else
                    {
                        // make sure text selected matches the search string
                        if (txtControl.SelectedText == searchString)
                        {
                            // replace text and substract or add to index based on replace string's length
                            txtControl.SelectedText = replaceString;
                            _currentIndex -= (searchString.Length - replaceString.Length);

                            // find the next occurrence
                            _currentIndex = txtControl.Text.IndexOf(searchString, _currentIndex);

                            // if one is found, jump to and select it so user can easily replace it aswell
                            if (_currentIndex >= 0)
                            {
                                FindNext(searchString, true, txtControl, false);
                            }
                            else
                            {
                                MessageBox.Show("No matches, reached the end of the document.");
                                _currentIndex = 0;
                                return;
                            }
                        }
                        else
                        {
                            MessageBox.Show("No matches, reached the end of the document.");
                            _currentIndex = 0;
                            return;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region Case-Insensitive & Regular Expression

                    if (useRegularExpression)
                    {
                        try
                        {
                            // create a new expression
                            regularExpression = new Regex(searchString, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Invalid Regular Expression!");
                            return;
                        }

                        // match against current selection
                        var match = regularExpression.Match(txtControl.SelectedText);

                        if (match.Success)
                        {
                            // replace text and substract from index if chars were removed
                            replaceString = Regex.Replace(replaceString, @"\\n", "\n");
                            var replacedString = regularExpression.Replace(txtControl.SelectedText, replaceString);
                            txtControl.SelectedText = replacedString;
                            _currentIndex -= (match.Length - replacedString.Length);

                            // check for the next occurrence
                            var match2 = regularExpression.Match(txtControl.Text, _currentIndex);
                            if (match2.Success)
                            {
                                _currentIndex = match2.Index;
                                // jump to it
                                FindNext(searchString, false, txtControl, true);
                            }
                            else
                            {
                                MessageBox.Show("No matches, reached the end of the document.");
                                _currentIndex = 0;
                                return;
                            }
                        }
                        else
                        {
                            MessageBox.Show("No matches, reached the end of the document.");
                            _currentIndex = 0;
                            return;
                        }
                    }

                    #endregion

                    #region Case-Insensitive & Basic

                    else
                    {
                        // make sure text currently selected matches the search string
                        var culture = new CultureInfo("en-us");

                        if (culture.CompareInfo.Compare(searchString, txtControl.SelectedText, CompareOptions.IgnoreCase) == 0)
                        {
                            // replace text and substract from index if chars were removed
                            txtControl.SelectedText = replaceString;
                            _currentIndex -= (searchString.Length - replaceString.Length);

                            // if we went below the start, reset position to 0
                            if (_currentIndex < 0)
                            {
                                _currentIndex = 0;
                            }

                            // find the next occurrence
                            _currentIndex = culture.CompareInfo.IndexOf(txtControl.Text, searchString, _currentIndex, CompareOptions.IgnoreCase);
                            if (_currentIndex >= 0)
                            {
                                // if one found, jump to it
                                FindNext(searchString, false, txtControl, false);
                            }
                            else
                            {
                                MessageBox.Show("No matches, reached the end of the document.");
                                _currentIndex = 0;
                                return;
                            }
                        }
                        else
                        {
                            MessageBox.Show("No matches, reached the end of the document.");
                            _currentIndex = 0;
                            return;
                        }
                    }

                    #endregion
                }
            }
            else
            {
                // reset index and search for a new occurrence from the beginning
                _currentIndex = 0;
                FindNext(searchString, caseSensitive, txtControl, useRegularExpression);
            }
        }

        // function for searching through the whole txtControl and replacing all occurrences
        public static void ReplaceAll(string searchString, string replaceString, bool caseSensitive, RichTextBox txtControl, bool useRegularExpression)
        {
            // make sure editor is not empty
            if (string.IsNullOrEmpty(txtControl.Text.Trim()) || string.IsNullOrEmpty(searchString))
            {
                return;
            }

            Regex regularExpression = null;

            // get the length of the search string
            var searchLength = searchString.Length;
            _currentIndex = 0;

            // basic loop for searching through the whole editor
            do
            {
                // first find an occurrence


                #region Case Sensitive

                if (caseSensitive)
                {
                    if (useRegularExpression)
                    {
                        // we are using regular expressions, create a RegularExpression object
                        try
                        {
                            regularExpression = new Regex(searchString);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Invalid Regular Expression!");
                            return;
                        }

                        // expression fine, let's try to match it starting from the current position
                        var match = regularExpression.Match(txtControl.Text, _currentIndex);

                        // if matched successfully, get the index location of the match inside
                        // the textbox control and the length of the match
                        if (match.Success)
                        {
                            _currentIndex = match.Index;
                            searchLength = match.Length;
                        }
                        else
                        {
                            // no match
                            _currentIndex = -1;
                        }
                    }
                    else
                    {
                        // not a regular expression search, just match the literal string
                        _currentIndex = txtControl.Text.IndexOf(searchString, _currentIndex);
                    }
                }

                #endregion

                #region Case-Insensitive

                else
                {
                    // this section is for case-insensitive searches
                    if (useRegularExpression)
                    {
                        try
                        {
                            // set the ignore case option and multiline option for regular expressions
                            regularExpression = new Regex(searchString, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Invalid Regular expression!");
                            return;
                        }

                        // expression fine, let's try to match it starting from the current position
                        var match = regularExpression.Match(txtControl.Text, _currentIndex);

                        // if matched successfully, get the index location of the match inside
                        // the textbox control and the length of the match
                        if (match.Success)
                        {
                            _currentIndex = match.Index;
                            searchLength = match.Length;
                        }
                        else
                        {
                            // no match
                            _currentIndex = -1;
                        }
                    }
                    else
                    {
                        // this search is for basic, non-regular expression search which is also case-insensitive
                        _currentIndex = Culture.CompareInfo.IndexOf(txtControl.Text, searchString, _currentIndex, CompareOptions.IgnoreCase);
                    }
                }

                #endregion


                // if we found a match, select and replace it in the textbox
                if (_currentIndex >= 0)
                {
                    // select the matching text
                    txtControl.SelectionStart = txtControl.Text.IndexOf("\n", _currentIndex) + 2;
                    txtControl.SelectionLength = 0;
                    txtControl.SelectionStart = _currentIndex;
                    txtControl.SelectionLength = searchLength;

                    // replace the found occurrence
                    string replacedString;
                    if (useRegularExpression)
                    {
                        // remove extra slashes from line-breaks
                        replaceString = Regex.Replace(replaceString, @"\\n", "\n");
                        replacedString = regularExpression.Replace(txtControl.SelectedText, replaceString);
                    }
                    else
                    {
                        replacedString = replaceString;
                    }
                    txtControl.SelectedText = replacedString;

                    // advance past selection, or substract if removed text
                    _currentIndex += replacedString.Length;
                    txtControl.SelectionLength = 0;
                }
            }

            // do as long as searchString is found
            while (_currentIndex >= 0);

            // reset position
            _currentIndex = 0;
        }
    }
}