import { toCsv } from './report.service';

describe('toCsv', () => {
  it('joins rows with CRLF and leaves plain values alone', () => {
    expect(toCsv(['HSN', 'Total'], [['3004', 105.5]])).toBe('HSN,Total\r\n3004,105.5');
  });

  it('quotes commas, quotes and line breaks, and doubles quotes', () => {
    const csv = toCsv(['Medicine'], [['Paracip, 650 mg'], ['Syrup "cherry"'], ['two\nlines']]);

    expect(csv).toBe('Medicine\r\n"Paracip, 650 mg"\r\n"Syrup ""cherry"""\r\n"two\nlines"');
  });

  it('writes empty cells for null', () => {
    expect(toCsv(['A', 'B'], [[null, 'x']])).toBe('A,B\r\n,x');
  });
});
