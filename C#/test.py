import json

ausgang = [0]

time = [12500]
channel = [5]
value = [100]

# dX = neuerWert - ausgangsWert

eA = []

for i in range(len(time)):
    t = time[i]
    c = channel[i]
    v = value[i]
    a = ausgang[i] #Should use channel

    diffPL = (v-a)/(t/100) #Difference per loop
    print(diffPL)

    s = []
    for j in range(1, int(t/100)+1):
        s.append(c)
        s.append(round(diffPL*j)+a)
    eA.append(s)

with open("see.json", "w") as file:
    json.dump(eA, file)

def test(t: str="Hallo") -> str: #test.__annotations__ = {'t': <class 'str'>, 'return': <class 'str'>}  (Its easyer to understand what you should pass in :)
    print(t)

#Dict -> List -> Listen