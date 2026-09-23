const fs = require('fs');

const CSV = 'F:/flywire/FAFB-v783/neuropil_synapse_table.csv';
const OUT = 'g:/flywire/src/FlywireAI/FAFBv783/NeuropilSynapseTable.vb';

const fd = fs.openSync(CSV, 'r');
const buf = Buffer.alloc(64 * 1024);
const n = fs.readSync(fd, buf, 0, buf.length, 0);
fs.closeSync(fd);

const header = buf.slice(0, n).toString('utf8').split('\n')[0].replace(/\r$/, '');
const columns = header.split(',');

if (columns.length !== 321) {
    throw new Error('unexpected column number: ' + columns.length);
}

function pascalToken(token) {
    if (/^[a-z0-9_]+$/.test(token)) {
        return token
            .split('_')
            .map((w) => (w ? w.charAt(0).toUpperCase() + w.slice(1) : w))
            .join('');
    }
    return token.charAt(0).toUpperCase() + token.slice(1);
}

function propertyName(column) {
    return column.split(' ').map(pascalToken).join('');
}

const rows = [];
const seen = new Set();

for (let i = 0; i < columns.length; i++) {
    const column = columns[i];
    const name = propertyName(column);
    if (seen.has(name)) {
        throw new Error('duplicated property name ' + name + ' at column ' + (i + 1));
    }
    seen.add(name);

    const type = i === 0 ? 'Long' : 'Double';
    rows.push(`        <Column("${column}")>`);
    rows.push(`        Public Property ${name} As ${type}`);
    rows.push('');
}

const body = rows.join('\n').replace(/\n+$/, '\n');

const vb = `Imports Microsoft.VisualBasic.Data.Framework.StorageProvider.Reflection

Namespace FAFBv783

    ''' <summary>
    ''' Per Neuropil Connection And Synapse Counts [Original Version Used Prior To July 2025]:
    ''' \`\`neuropil_synapse_table.csv.gz\`\` (134,181 rows, 321 columns).
    ''' 
    ''' In-out synapse &amp; partner counts by neuropil. For every cell and neuropil
    ''' (region), contains the number of input and output synapses, as well as the
    ''' number of input and output partners the cell has in that neuropil.
    ''' </summary>
    ''' <remarks>
    ''' Note: this resource is a convenience, it can be derived from the connectivity
    ''' table.
    ''' 
    ''' The count columns are declared in the column order of the original csv document,
    ''' that is from col 2 to col 321, because several column names of this table
    ''' contain blank characters (for example \`\`input synapses in AL_L\`\`), the
    ''' <see cref="ColumnAttribute"/> alias is required for every of them.
    ''' </remarks>
    Public Class NeuropilSynapseTable

        ''' <summary>
        ''' FlyWire Root ID of the cell.
        ''' </summary>
${body}
    End Class
End Namespace
`;

fs.writeFileSync(OUT, vb, 'utf8');
console.log('written:', OUT, vb.split('\n').length, 'lines, columns:', columns.length);
