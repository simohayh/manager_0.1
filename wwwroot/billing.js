// Bill maths for split bills. Pure functions (no page access), all money in whole cents so nothing is lost to rounding.
// Used by cashier.js. Works in the browser and in node (for the tests).

const Billing = (() => {
    const toCents = amount => Math.round((Number(amount) || 0) * 100);

    /** Splits `totalCents` into `n` parts that add up exactly; the leftover cents go one each to the first parts. */
    function splitEqual(totalCents, n) {
        n = Math.max(1, Math.floor(n));
        const base = Math.trunc(totalCents / n);
        let rest = totalCents - base * n;
        return Array.from({ length: n }, () => {
            const extra = rest > 0 ? 1 : (rest < 0 ? -1 : 0);
            rest -= extra;
            return base + extra;
        });
    }

    /** Shares `amountCents` between people in proportion to `weights` (largest-remainder, adds up exactly). */
    function shareProportionally(amountCents, weights) {
        const total = weights.reduce((a, b) => a + b, 0);
        if (total <= 0 || amountCents === 0) return weights.map(() => 0);

        const raw = weights.map(w => amountCents * w / total);
        const floors = raw.map(Math.floor);
        let leftover = amountCents - floors.reduce((a, b) => a + b, 0);

        const order = raw.map((r, i) => ({ i, frac: r - Math.floor(r) })).sort((a, b) => b.frac - a.frac || a.i - b.i);
        for (let k = 0; leftover > 0 && k < order.length; k++, leftover--) floors[order[k].i] += 1;
        return floors;
    }

    /**
     * lines: [{ name, quantity, price }]  (real dishes only)
     * discountCents: total discount on the table (positive number, 0 if none)
     * assignment: for each line, the person number 0..n-1 who pays for it
     * Returns one entry per person: { lines, subtotal, discount, total } (cents).
     */
    function splitByItems(lines, assignment, n, discountCents) {
        const people = Array.from({ length: n }, () => ({ lines: [], subtotal: 0, discount: 0, total: 0 }));
        lines.forEach((line, idx) => {
            const p = people[Math.min(n - 1, Math.max(0, assignment[idx] | 0))];
            p.lines.push(line);
            p.subtotal += toCents(line.price) * line.quantity;
        });

        const shares = shareProportionally(discountCents || 0, people.map(p => p.subtotal));
        people.forEach((p, i) => { p.discount = shares[i]; p.total = p.subtotal - shares[i]; });
        return people;
    }

    return { toCents, splitEqual, shareProportionally, splitByItems };
})();

if (typeof module !== 'undefined') module.exports = Billing;
