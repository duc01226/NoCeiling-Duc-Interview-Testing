// @type-check
const posthtml = require('posthtml');
const sorter = require('posthtml-attrs-sorter');
const beautify = require('js-beautify');
const fs = require('fs');
const glob = require('glob');

const argv = require('yargs-parser')(process.argv.slice(2));
const attrSortOptions = {
  order: [
    '\\#\\w+',
    '\\*ngIf',
    '\\*\\w+',
    'class',
    'id',
    'name',
    'data-.+',
    'src',
    'for',
    'type',
    'href',
    'values',
    'title',
    'alt',
    'role',
    'aria-.+',
    '\\[ngClass]',
    '\\[class.[\\w\\-]+\\]',
    '\\[ngStyle]',
    '\\[style.[\\w\\-]+\\]',
    '\\[\\(\\w+\\)\\]',
    '\\[\\w+\\]',
    '\\(\\w+\\)',
    'let-.+',
    '$unknown$',
  ],
};
const jsBeautyOptions = {
  indent_size: 2,
  eol: '\r\n',
  wrap_attributes: 'force-aligned',
  end_with_newline: false,
  wrap_line_length: 140,
};

const files = [];

if (argv.staged) {
  files.push(...process.argv.slice(3));
} else {
  files.push(
    ...glob.sync('**/*.component.html', {
      ignore: ['**/node_modules/**'],
    })
  );
}

var promises = files
  .filter((file) => file.endsWith('.component.html'))
  .map((file) =>
    Promise.resolve().then(() => {
      const text = fs.readFileSync(file, 'utf8');
      let result = posthtml()
        .use(sorter(attrSortOptions))
        .process(text, { sync: true }).html;
      result = beautify.html(result, jsBeautyOptions);
      result = result.replace(/\=\"\"/gi, '');
      fs.writeFileSync(file, result + '\r\n', 'utf8');
      console.log(`Formatted ${file}`);
    })
  );

Promise.all(promises).then(() => console.log('SUCCESS'));
