find . -type f -name '*.cs' -print0 |
while IFS= read -r -d '' file; do
    dir=$(dirname "$file")
    lines=$(wc -l < "$file")
    printf '%s %s\n' "$dir" "$lines"
done |
awk '{sum[$1]+=$2} END {for (dir in sum) print sum[dir], dir}' |
sort -rn
