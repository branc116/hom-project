
# $exeName = ;

$outFolder = "../resoults"
#Run tests
1..6 | % -Parallel { 
    $outFolder = "../resoults";
    $instancesFolder = "../Sets"
    ../bin/Release/net6.0/Hom.Projekt.exe  "$instancesFolder/i$_.txt" "$outFolder" "0,93" "i$_"
} -ThrottleLimit 6 -TimeoutSeconds 86400
#Generate images
ls $outFolder | ? name -like *.dot | % -Parallel {
    $imgsFolder = "../imgs"
    dot -Kfdp -n -Tsvg -o "$imgsFolder/$($_.Name).svg" $_.FullName
} -ThrottleLimit 8
#validate
1..6 | % {
    $outFolder = "../resoults";
    $num = $_;
    ls $outFolder | ? name -like "*i$_*.txt" | % {
        python3 ../Validator/validator.py -i "../Sets/i$num.txt" -o "$($_.FullName)"
    }
}
