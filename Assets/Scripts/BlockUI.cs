using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler ,IDropHandler
{
    private string key;
   
    public GameObject move_prefab;

    GameObject move_image;

    private bool complete = false;



    public void SetKey(string n) { key = n; }

    private Vector2 SetSizeNorm(Vector2 size, float n)
    {
        float stand = size.x;
        if (size.y > size.x) stand = size.y;
        size /= stand;
        return size * n;
    }

    IEnumerator FadeInOut()
    {

        yield return new WaitForSeconds(0.1f);
    }


    private void Start()
    {
        transform.localScale = SetSizeNorm(GameManager.Instance.Puzzle[key].ui.bounds.size, 1);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        //µå·¡±× ÁßÀÎ ÆÛÁñ key
        GameManager.Instance.drag_block_id = key;

        //moveImage »õ·Î »ý¼º
        GameManager.Instance.move_canvas.SetActive(true);
        move_prefab.GetComponent<Image>().sprite = GameManager.Instance.Puzzle[key].ui;
        move_image = Instantiate<GameObject>(move_prefab, GameManager.Instance.move_canvas.transform);
        move_image.transform.position = transform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        move_image.transform.position = eventData.position;
        move_image.transform.localScale = transform.localScale;
        Debug.Log(Vector2.Distance(GameManager.Instance.Puzzle[key].pos, move_image.transform.position));
        if (Vector2.Distance(GameManager.Instance.Puzzle[key].pos, move_image.transform.position) <= GameManager.Instance.block_distance)
        {
            //ºþÂ¦ÀÓ
            complete = true;
        }
        else
        {
            //ºþÂ¦ÀÓ ÄÚ·çÆ¾ ¸ØÃã
            complete = false;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (complete)
        {
            GameManager.Instance.Puzzle[key].block.SetActive(true);
            Destroy(move_image.gameObject);
            move_image = null;
            Destroy(gameObject);
        }
        else
        {
            Destroy(move_image.gameObject);
            move_image = null;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {

    }

}
